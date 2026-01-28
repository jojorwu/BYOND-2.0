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

    public class Game
    {
        private IWindow _window;
        private GL _gl;
        private LogicThread _logicThread;
        private SpriteRenderer _spriteRenderer;
        private ModelRenderer _modelRenderer;
        private CSharpShaderManager _csharpShaderManager;
        private ICSharpShader? _sampleCSharpShader;
        private Graphics.Shader? _sampleGlShader;
        private Mesh _cubeMesh;
        private Camera _camera;
        private ImGuiController _imGuiController;
        private ConnectionPanel _connectionPanel;

        private ClientState _clientState = ClientState.Connecting;
        private GameState _previousState;
        private GameState _currentState;
        private double _accumulator;
        private float _alpha;

        public Game()
        {
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

            TextureCache.Init(_gl);
            _spriteRenderer = new SpriteRenderer(_gl);
            _modelRenderer = new ModelRenderer(_gl);
            _csharpShaderManager = new CSharpShaderManager(_gl);
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
            } catch (Exception e) {
                Console.WriteLine($"Failed to load C# shader: {e.Message}");
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
                }

                _alpha = (float)(_accumulator / LogicThread.TimeStep);

                if (_currentState?.GameObjects != null)
                {
                    foreach (var currentObj in _currentState.GameObjects.Values)
                    {
                        var icon = GetIcon(currentObj);
                        if (!string.IsNullOrEmpty(icon))
                        {
                            var (dmiPath, stateName) = IconCache.ParseIconString(icon);
                            if(!string.IsNullOrEmpty(dmiPath))
                            {
                                try
                                {
                                    var texture = TextureCache.GetTexture(dmiPath.Replace(".dmi", ".png"));
                                    DmiCache.GetDmi(dmiPath, texture);
                                }
                                catch (Exception e)
                                {
                                    // Ignore errors here, they will be handled in OnRender
                                }
                            }
                        }
                    }
                }
            }
        }

        private float _time;

        private void OnRender(double deltaTime)
        {
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
                var projection = _camera.GetProjectionMatrix((float)_window.FramebufferSize.X / _window.FramebufferSize.Y);
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
                    foreach (var currentObj in _currentState.GameObjects.Values)
                    {
                        Vector2 renderPos;

                        var currentPosition = GetPosition(currentObj);
                        if (_previousState?.GameObjects != null && _previousState.GameObjects.TryGetValue(currentObj.Id, out var previousObj))
                        {
                            var previousPosition = GetPosition(previousObj);
                            renderPos = Vector2.Lerp(previousPosition, currentPosition, _alpha);
                        }
                        else
                        {
                            renderPos = currentPosition;
                        }

                        var icon = GetIcon(currentObj);
                        if (!string.IsNullOrEmpty(icon))
                        {
                            var (dmiPath, stateName) = IconCache.ParseIconString(icon);
                            if(!string.IsNullOrEmpty(dmiPath))
                            {
                                try
                                {
                                    var texture = TextureCache.GetTexture(dmiPath.Replace(".dmi", ".png"));
                                    var dmi = DmiCache.GetDmi(dmiPath, texture);
                                    var state = dmi.Description.GetStateOrDefault(stateName);
                                    if(state != null)
                                    {
                                        var frame = state.GetFrames(AtomDirection.South)[0];
                                        var uv = new Box2(
                                            (float)frame.X / dmi.Width,
                                            (float)frame.Y / dmi.Height,
                                            (float)(frame.X + dmi.Description.Width) / dmi.Width,
                                            (float)(frame.Y + dmi.Description.Height) / dmi.Height
                                        );

                                        _spriteRenderer.Draw(dmi.TextureId, uv, renderPos * 32, new Vector2(32, 32), Color.White, GetLayer(currentObj));
                                    }
                                }
                                catch (Exception)
                                {
                                    _spriteRenderer.DrawQuad(renderPos * 32, new Vector2(32, 32), Color.Red, GetLayer(currentObj)); // Draw red square on error
                                }
                            } else {
                                _spriteRenderer.DrawQuad(renderPos * 32, new Vector2(32, 32), Color.Magenta, GetLayer(currentObj)); // Draw magenta square for missing dmi
                            }
                        } else {
                             _spriteRenderer.DrawQuad(renderPos * 32, new Vector2(32, 32), Color.White, GetLayer(currentObj));
                        }
                    }
                }

                _spriteRenderer.End();
            }

            _imGuiController.Render();
        }

        private Vector2 GetPosition(GameObject obj)
        {
            return new Vector2(obj.X, obj.Y);
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
            TextureCache.Dispose();
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
                Console.WriteLine("Sending ping...");
                _logicThread?.SendCommand("ping");
            }
        }
    }
}
