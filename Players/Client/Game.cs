using Silk.NET.Windowing;
using Silk.NET.Input;
using Silk.NET.OpenGL;
using System;
using Client.Graphics;
using Robust.Shared.Maths;
using Core;
using Core.Dmi;
using System.Numerics;
using System.Text.Json;
using Client.Assets;
using Client.UI;
using ImGuiNET;
using Silk.NET.OpenGL.Extensions.ImGui;
using Shared;
using System.Collections.Generic;

namespace Client
{
    public enum ClientState
    {
        Connecting,
        InGame
    }

    public class Game : IClient
    {
        private IWindow _window;
        private GL _gl = null!;
        private LogicThread _logicThread = null!;
        private SpriteRenderer _spriteRenderer = null!;
        private ModelRenderer _modelRenderer = null!;
        private readonly TextureCache _textureCache;
        private readonly CSharpShaderManager _csharpShaderManager;
        private readonly DmiCache _dmiCache;
        private readonly IconCache _iconCache;
        private ICSharpShader? _sampleCSharpShader;
        private Graphics.Shader? _sampleGlShader;
        private Mesh _cubeMesh = null!;
        private Camera _camera = null!;
        private ImGuiController _imGuiController = null!;
        private ConnectionPanel _connectionPanel = null!;
        private MainHud _mainHud = null!;

        private ClientState _clientState = ClientState.Connecting;
        private GameState _previousState = null!;
        private GameState _currentState = null!;
        private GameObject? _playerObject;
        private double _accumulator;
        private float _alpha;

        public Game(TextureCache textureCache, CSharpShaderManager csharpShaderManager, DmiCache dmiCache, IconCache iconCache)
        {
            _textureCache = textureCache;
            _csharpShaderManager = csharpShaderManager;
            _dmiCache = dmiCache;
            _iconCache = iconCache;

            var options = WindowOptions.Default;
            options.Title = "BYOND 2.0 Client";
            options.Size = new Silk.NET.Maths.Vector2D<int>(1280, 720);
            _window = Window.Create(options);

            _window.Load += OnLoad;
            _window.Update += OnUpdate;
            _window.Render += OnRender;
            _window.Closing += OnClose;
        }

        public void Run()
        {
            _window.Run();
        }

        private void OnLoad()
        {
            IInputContext input = _window.CreateInput();
            for (int i = 0; i < input.Keyboards.Count; i++)
            {
                input.Keyboards[i].KeyDown += KeyDown;
            }

            _gl = GL.GetApi(_window);
            _imGuiController = new ImGuiController(_gl, _window, input);
            _connectionPanel = new ConnectionPanel();
            _mainHud = new MainHud();
            _mainHud.AddMessage("Welcome to BYOND 2.0!");
            _mainHud.AddMessage("Connecting to server...");

            UiTheme.Apply();

            _textureCache.SetGL(_gl);
            _csharpShaderManager.SetGL(_gl);
            _spriteRenderer = new SpriteRenderer(_gl);
            _modelRenderer = new ModelRenderer(_gl);
            _cubeMesh = MeshFactory.CreateCube(_gl);
            _camera = new Camera(new Vector2(0, 0), 1.0f);

            LoadCSharpShader();
        }

        private async void LoadCSharpShader()
        {
            string code = @"
using System;
using System.Numerics;
using Client.Graphics;

public class MyShader : ICSharpShader
{
    public string GetVertexSource() => @""#version 330 core
layout (location = 0) in vec3 aPosition;
layout (location = 1) in vec3 aNormal;
layout (location = 2) in vec2 aTexCoords;
out vec2 TexCoords;
uniform mat4 uModel;
uniform mat4 uView;
uniform mat4 uProjection;
void main() {
    TexCoords = aTexCoords;
    gl_Position = uProjection * uView * uModel * vec4(aPosition, 1.0);
}"";

    public string GetFragmentSource() => @""#version 330 core
out vec4 FragColor;
in vec2 TexCoords;
uniform sampler2D uTexture;
uniform float uTime;
void main() {
    vec4 texColor = texture(uTexture, TexCoords);
    FragColor = texColor * vec4(sin(uTime) * 0.5 + 0.5, cos(uTime) * 0.5 + 0.5, 1.0, 1.0);
}"";

    public void Setup(Graphics.Shader shader) { }
    public void Update(Graphics.Shader shader, float deltaTime) {
         shader.SetUniform(""uTime"", (float)DateTime.Now.TimeOfDay.TotalSeconds);
    }
}
new MyShader()
";
            try {
                _sampleCSharpShader = await _csharpShaderManager.CompileShaderAsync(code);
                _sampleGlShader = _csharpShaderManager.CreateGlShader(_sampleCSharpShader);
            } catch (Exception ex) {
                Console.WriteLine($"Failed to load C# shader: {ex.Message}");
            }
        }

        private void OnUpdate(double deltaTime)
        {
            _imGuiController.Update((float)deltaTime);

            if (_clientState == ClientState.Connecting)
            {
                if (_connectionPanel.IsConnectRequested)
                {
                    _connectionPanel.IsConnectRequested = false;
                    _logicThread = new LogicThread(_connectionPanel.ServerAddress);
                    _previousState = _logicThread.PreviousState;
                    _currentState = _logicThread.CurrentState;
                    _logicThread.Start();
                    _clientState = ClientState.InGame;
                }
            }
            else if (_clientState == ClientState.InGame)
            {
                _accumulator += deltaTime;

                while (_accumulator >= LogicThread.TimeStep)
                {
                    var states = _logicThread.GetStatesForRender();
                    _previousState = states.Item1;
                    _currentState = states.Item2;
                    _accumulator -= LogicThread.TimeStep;

                    UpdatePlayerObject();
                }

                _alpha = (float)(_accumulator / LogicThread.TimeStep);

                if (_currentState?.GameObjects != null)
                {
                    // Parallel pre-fetching of assets to leverage multiple cores
                    Parallel.ForEach(_currentState.GameObjects.Values, currentObj =>
                    {
                        var icon = GetIcon(currentObj);
                        if (!string.IsNullOrEmpty(icon))
                        {
                            var (dmiPath, _) = _iconCache.ParseIconString(icon);
                            if (!string.IsNullOrEmpty(dmiPath))
                            {
                                try
                                {
                                    var texture = _textureCache.GetTexture(dmiPath.Replace(".dmi", ".png"));
                                    _dmiCache.GetDmi(dmiPath, texture);
                                }
                                catch (Exception)
                                {
                                    // Assets will be handled in OnRender if they fail here
                                }
                            }
                        }
                    });
                }
            }
        }

        private float _time;

        private void OnRender(double deltaTime)
        {
            _textureCache.ProcessUploads(); // Upload newly loaded textures to GPU

            _time += (float)deltaTime;
            _gl.ClearColor(0.2f, 0.3f, 0.3f, 1.0f);
            _gl.Clear(ClearBufferMask.ColorBufferBit | ClearBufferMask.DepthBufferBit);

            if (_clientState == ClientState.Connecting)
            {
                _connectionPanel.Draw();
            }
            else if (_clientState == ClientState.InGame)
            {
                // Render 3D test
                _gl.Enable(EnableCap.DepthTest);
                var view = _camera.GetViewMatrix();
                var projection = _camera.GetProjectionMatrix((float)_window.FramebufferSize.X, (float)_window.FramebufferSize.Y);
                var model = Matrix4x4.CreateRotationY(_time) * Matrix4x4.CreateRotationX(_time * 0.5f) * Matrix4x4.CreateScale(100.0f);

                if (_sampleGlShader != null && _sampleCSharpShader != null)
                {
                    _sampleGlShader.Use();
                    _sampleGlShader.SetUniform("uModel", model);
                    _sampleGlShader.SetUniform("uView", view);
                    _sampleGlShader.SetUniform("uProjection", projection);
                    _sampleCSharpShader.Update(_sampleGlShader, (float)deltaTime);
                    _cubeMesh.Draw();
                }
                else
                {
                    _modelRenderer.Render(_cubeMesh, 0, model, view, projection, Vector3.One);
                }

                _gl.Disable(EnableCap.DepthTest);

                _spriteRenderer.Begin(view, projection);

                if (_currentState?.GameObjects != null)
                {
                    // Calculate view bounds for culling (in world/tile units)
                    var viewWidthTiles = (_window.FramebufferSize.X / _camera.Zoom) / 32.0f;
                    var viewHeightTiles = (_window.FramebufferSize.Y / _camera.Zoom) / 32.0f;
                    var cameraTilePos = _camera.Position / 32.0f;
                    var cullRect = new Box2(
                        cameraTilePos.X - viewWidthTiles / 2 - 1,
                        cameraTilePos.Y - viewHeightTiles / 2 - 1,
                        cameraTilePos.X + viewWidthTiles / 2 + 1,
                        cameraTilePos.Y + viewHeightTiles / 2 + 1
                    );

                    // Prepare data for optimized culling
                    var gameObjects = new List<GameObject>(_currentState.GameObjects.Values);
                    var positions = new Vector2[gameObjects.Count];
                    var visibilityMask = new byte[gameObjects.Count];

                    // Parallel position calculation and interpolation
                    Parallel.For(0, gameObjects.Count, i =>
                    {
                        var currentObj = gameObjects[i];
                        var currentPosition = GetPosition(currentObj);

                        if (_previousState?.GameObjects != null && _previousState.GameObjects.TryGetValue(currentObj.Id, out var previousObj))
                        {
                            positions[i] = Vector2.Lerp(GetPosition(previousObj), currentPosition, _alpha);
                        }
                        else
                        {
                            positions[i] = currentPosition;
                        }
                    });

                    // Optimized visibility culling
                    VisibilityCuller.CalculateVisibilityOptimized(positions, cullRect, visibilityMask);

                    // Parallel command generation for visible objects
                    Parallel.For(0, gameObjects.Count, i =>
                    {
                        if (visibilityMask[i] == 0) return;

                        var currentObj = gameObjects[i];
                        var renderPos = positions[i];

                        var layer = GetLayer(currentObj);
                        var icon = GetIcon(currentObj);

                        if (!string.IsNullOrEmpty(icon))
                        {
                            var (dmiPath, stateName) = _iconCache.ParseIconString(icon);
                            if (!string.IsNullOrEmpty(dmiPath))
                            {
                                try
                                {
                                    var texture = _textureCache.GetTexture(dmiPath.Replace(".dmi", ".png"));
                                    var dmi = _dmiCache.GetDmi(dmiPath, texture);
                                    if (dmi != null)
                                    {
                                        var state = dmi.Description.GetStateOrDefault(stateName);
                                        if (state != null)
                                        {
                                            var frame = state.GetFrames(AtomDirection.South)[0];
                                            var uv = new Box2(
                                                (float)frame.X / dmi.Width,
                                                (float)frame.Y / dmi.Height,
                                                (float)(frame.X + dmi.Description.Width) / dmi.Width,
                                                (float)(frame.Y + dmi.Description.Height) / dmi.Height
                                            );

                                            _spriteRenderer.Draw(dmi.TextureId, uv, renderPos * 32, new Vector2(32, 32), Color.White, layer);
                                        }
                                    }
                                    else
                                    {
                                        _spriteRenderer.DrawQuad(renderPos * 32, new Vector2(32, 32), Color.Gray, layer); // Loading placeholder
                                    }
                                }
                                catch (Exception)
                                {
                                    _spriteRenderer.DrawQuad(renderPos * 32, new Vector2(32, 32), Color.Red, layer);
                                }
                            }
                            else
                            {
                                _spriteRenderer.DrawQuad(renderPos * 32, new Vector2(32, 32), Color.Magenta, layer);
                            }
                        }
                        else
                        {
                            _spriteRenderer.DrawQuad(renderPos * 32, new Vector2(32, 32), Color.White, layer);
                        }
                    });
                }

                _spriteRenderer.End();

                _mainHud.Draw(_playerObject);
            }

            _imGuiController.Render();
        }

        private Vector2 GetPosition(GameObject obj)
        {
            return new Vector2(obj.X, obj.Y);
        }

        private void UpdatePlayerObject()
        {
            if (_currentState?.GameObjects != null)
            {
                // In a real scenario, the server would tell us our player ID.
                // For now, we'll just find the first object of type "player".
                foreach (var obj in _currentState.GameObjects.Values)
                {
                    if (obj.ObjectType?.Name == "player")
                    {
                        _playerObject = obj;
                        return;
                    }
                }
            }
            _playerObject = null;
        }

        private float GetLayer(GameObject obj)
        {
            var layer = obj.GetVariable("layer");
            if (layer.Type == DreamValueType.Float)
            {
                return layer.AsFloat();
            }
            return 2.0f; // Default atom layer
        }

        private string? GetIcon(GameObject obj)
        {
            var icon = obj.GetVariable("Icon");
            if (icon.Type == DreamValueType.String && icon.TryGetValue(out string? iconStr))
            {
                return iconStr;
            }
            return null;
        }

        private void OnClose()
        {
            _logicThread?.Stop();
            _spriteRenderer.Dispose();
            _modelRenderer.Dispose();
            _sampleGlShader?.Dispose();
            _cubeMesh.Dispose();
            _imGuiController.Dispose();
        }

        private void KeyDown(IKeyboard keyboard, Key key, int arg3)
        {
            if (key == Key.Escape)
            {
                _window.Close();
            }
            if (key == Key.P)
            {
                _mainHud.AddMessage("Sending ping...");
                _logicThread?.SendCommand("ping");
            }
        }
    }
}
