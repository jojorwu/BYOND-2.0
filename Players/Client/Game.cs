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
using Shared.Interfaces;
using Shared;
using Shared.Config;
using Shared.Enums;
using Shared.Interfaces;
using Microsoft.Extensions.DependencyInjection;
using System.Collections.Concurrent;
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
        private readonly IObjectTypeManager _typeManager;
        private readonly IObjectFactory _objectFactory;
        private IWindow _window;
        private GL? _gl;
        private Client.Graphics.Vulkan.VulkanContext? _vulkanContext;
        private LogicThread _logicThread = null!;
        private SpriteRenderer _spriteRenderer = null!;
        private ModelRenderer _modelRenderer = null!;
        private WorldRenderer _worldRenderer = null!;
        private LightingRenderer _lightingRenderer = null!;
        private OccluderMap _occluderMap = null!;
        private SSAOShader _ssaoShader = null!;
        private ParticleSystem _particleSystem = null!;
        private BloomShader _bloomShader = null!;
        private PostProcessShader _postProcessShader = null!;
        private GBuffer _gBuffer = null!;
        private Client.Graphics.Framebuffer _sceneFramebuffer = null!;
        private Client.Graphics.Framebuffer[] _bloomBuffers = new Client.Graphics.Framebuffer[2];
        private RenderPipeline _renderPipeline = null!;
        private readonly TextureCache _textureCache;
        private readonly CSharpShaderManager _csharpShaderManager;
        private readonly DmiCache _dmiCache;
        private readonly IconCache _iconCache;
        private readonly IServiceProvider _serviceProvider;
        private ICSharpShader? _sampleCSharpShader;
        private Graphics.Shader? _sampleGlShader;
        private Mesh _cubeMesh = null!;
        private Camera _camera = null!;
        private ImGuiController _imGuiController = null!;
        private ConnectionPanel _connectionPanel = null!;
        private MainHud _mainHud = null!;
        private SettingsPanel _settingsPanel = null!;
        private readonly ISoundSystem _soundSystem;
        private readonly IConfigurationManager _configManager;
        private readonly ClientLaunchOptions _launchOptions;

        private ClientState _clientState = ClientState.Connecting;
        private GameState _previousState = null!;
        private GameState _currentState = null!;
        private GameObject? _playerObject;
        private double _accumulator;
        private float _alpha;

        public Game(TextureCache textureCache, CSharpShaderManager csharpShaderManager, DmiCache dmiCache, IconCache iconCache, IObjectTypeManager typeManager, IObjectFactory objectFactory, ISoundSystem soundSystem, IConfigurationManager configManager, IServiceProvider serviceProvider, ClientLaunchOptions launchOptions)
        {
            _textureCache = textureCache;
            _soundSystem = soundSystem;
            _csharpShaderManager = csharpShaderManager;
            _dmiCache = dmiCache;
            _iconCache = iconCache;
            _typeManager = typeManager;
            _objectFactory = objectFactory;
            _configManager = configManager;
            _serviceProvider = serviceProvider;
            _launchOptions = launchOptions;

            _configManager.RegisterFromAssemblies(typeof(ConfigKeys).Assembly);
            _configManager.AddProvider(new JsonConfigProvider("client_config.json"));
            _configManager.LoadAll();

            var options = WindowOptions.Default;
            options.Title = "BYOND 2.0 Client";
            options.Size = new Silk.NET.Maths.Vector2D<int>(_configManager.GetCVar<int>(ConfigKeys.GraphicsResolutionX), _configManager.GetCVar<int>(ConfigKeys.GraphicsResolutionY));
            options.VSync = _configManager.GetCVar<bool>(ConfigKeys.GraphicsVSync);
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

            var backend = _configManager.GetCVar<string>(ConfigKeys.GraphicsBackend);
            if (backend.Equals("Vulkan", StringComparison.OrdinalIgnoreCase))
            {
                _vulkanContext = new Client.Graphics.Vulkan.VulkanContext(_window);
                // ImGui with Vulkan requires more setup, for now we initialize GL as fallback for UI if possible
                // but strictly following backend for logic
                _gl = GL.GetApi(_window);
            }
            else
            {
                _gl = GL.GetApi(_window);
            }

            _imGuiController = new ImGuiController(_gl, _window, input);
            _connectionPanel = new ConnectionPanel();
            if (!string.IsNullOrEmpty(_launchOptions.AutoConnectAddress))
            {
                _connectionPanel.ServerAddress = _launchOptions.AutoConnectAddress;
                _connectionPanel.IsConnectRequested = true;
            }
            _mainHud = new MainHud();
            _settingsPanel = new SettingsPanel(_configManager, _serviceProvider.GetRequiredService<IConsoleCommandManager>());
            _mainHud.AddMessage("Welcome to BYOND 2.0!");
            _mainHud.AddMessage("Connecting to server...");

            UiTheme.Apply();

            _textureCache.SetGL(_gl);
            _csharpShaderManager.SetGL(_gl);
            _spriteRenderer = new SpriteRenderer(_gl);
            _modelRenderer = new ModelRenderer(_gl);
            _worldRenderer = new WorldRenderer(_gl, _textureCache, _dmiCache, _iconCache);
            _lightingRenderer = new LightingRenderer(_gl);
            _occluderMap = new OccluderMap(_gl, _window.Size.X, _window.Size.Y);
            _ssaoShader = new SSAOShader(_gl);
            _particleSystem = new ParticleSystem(_gl);
            _bloomShader = new BloomShader(_gl);
            _postProcessShader = new PostProcessShader(_gl);
            _gBuffer = new GBuffer(_gl, _window.Size.X, _window.Size.Y);
            _sceneFramebuffer = new Client.Graphics.Framebuffer(_gl, _window.Size.X, _window.Size.Y);
            _bloomBuffers[0] = new Client.Graphics.Framebuffer(_gl, _window.Size.X / 2, _window.Size.Y / 2);
            _bloomBuffers[1] = new Client.Graphics.Framebuffer(_gl, _window.Size.X / 2, _window.Size.Y / 2);
            _cubeMesh = MeshFactory.CreateCube(_gl);
            _camera = new Camera(new Vector2(0, 0), 1.0f);

            _renderPipeline = new RenderPipeline();
            _renderPipeline.AddPass(new OccluderPass(_occluderMap, _spriteRenderer));
            _renderPipeline.AddPass(new GeometryPass(_gBuffer, _worldRenderer));
            _renderPipeline.AddPass(new LightingPass(_lightingRenderer, _gBuffer, _occluderMap, _sceneFramebuffer, _particleSystem));
            _renderPipeline.AddPass(new PostProcessPass(_ssaoShader, _bloomShader, _postProcessShader, _occluderMap, _gBuffer, _sceneFramebuffer, _bloomBuffers, _spriteRenderer, _configManager));

            _ = LoadCSharpShader();

            _configManager.OnCVarChanged += (key, value) =>
            {
                if (key == ConfigKeys.GraphicsResolutionX || key == ConfigKeys.GraphicsResolutionY)
                {
                    _window.Size = new Silk.NET.Maths.Vector2D<int>(_configManager.GetCVar<int>(ConfigKeys.GraphicsResolutionX), _configManager.GetCVar<int>(ConfigKeys.GraphicsResolutionY));
                }
            };
        }

        private async Task LoadCSharpShader()
        {
            string code = @"

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
            _particleSystem.Update((float)deltaTime);

            if (_playerObject != null)
            {
                _soundSystem.Update(new Vector3(_playerObject.X, _playerObject.Y, (float)_playerObject.Z), Vector3.UnitY);
            }

            if (_clientState == ClientState.Connecting)
            {
                if (_connectionPanel.IsConnectRequested)
                {
                    _connectionPanel.IsConnectRequested = false;
                    var gameState = new GameState();
                    _logicThread = new LogicThread(_connectionPanel.ServerAddress, gameState, _serviceProvider.GetRequiredService<ISnapshotManager>(), _serviceProvider.GetRequiredService<IStateInterpolator>(), _serviceProvider.GetRequiredService<IPacketDispatcher>(), _serviceProvider.GetServices<IPacketHandler>());
                    _logicThread.SoundReceived += (sound) => _soundSystem.Play(sound);
                    _logicThread.StopSoundReceived += (file, objId) => _soundSystem.Stop(file, objId);
                    _logicThread.CVarSyncReceived += (key, val) =>
                    {
                        if (_configManager is ConfigurationManager mgr) mgr.SetCVarDirect(key, val);
                        else _configManager.SetCVar(key, val);
                    };
                    _currentState = _logicThread.CurrentState;
                    _logicThread.Start();
                    _clientState = ClientState.InGame;
                }
            }
            else if (_clientState == ClientState.InGame)
            {
                _logicThread.UpdateRenderState();
                _currentState = _logicThread.GetStateForRender();

                _accumulator += deltaTime;

                while (_accumulator >= LogicThread.TimeStep)
                {
                    _accumulator -= LogicThread.TimeStep;
                    UpdatePlayerObject();
                }

                _alpha = (float)(_accumulator / LogicThread.TimeStep);

                if (_currentState?.GameObjects != null)
                {
                    // Update sound positions for attached objects
                    foreach (var obj in _currentState.GameObjects.Values)
                    {
                        _soundSystem.UpdateObjectPosition(obj.Id, new Vector3(obj.X, obj.Y, (float)obj.Z));
                    }

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
            _textureCache.ProcessUploads();

            _time += (float)deltaTime;

            if (_clientState == ClientState.Connecting)
            {
                if (_gl != null)
                {
                    _gl.ClearColor(0.2f, 0.3f, 0.3f, 1.0f);
                    _gl.Clear(ClearBufferMask.ColorBufferBit | ClearBufferMask.DepthBufferBit);
                }

                _connectionPanel.Draw();
            }
            else if (_clientState == ClientState.InGame)
            {
                var view = _camera.GetViewMatrix();
                var projection = _camera.GetProjectionMatrix((float)_window.FramebufferSize.X, (float)_window.FramebufferSize.Y);

                if (_currentState != null)
                {
                    var renderContext = new RenderContext(
                        _gl!,
                        null,
                        _currentState,
                        _alpha,
                        GetCullRect(),
                        view,
                        projection,
                        _window.FramebufferSize.X,
                        _window.FramebufferSize.Y
                    );

                    _renderPipeline.Execute(renderContext);
                }

                _mainHud.Draw(_playerObject);
            }

            _settingsPanel.Draw();

            if (_vulkanContext != null)
            {
                _vulkanContext.Render();
            }

            _imGuiController.Render();
        }

        private Box2 GetCullRect()
        {
            var viewWidthTiles = (_window.FramebufferSize.X / _camera.Zoom) / 32.0f;
            var viewHeightTiles = (_window.FramebufferSize.Y / _camera.Zoom) / 32.0f;
            var cameraTilePos = _camera.Position / 32.0f;
            return new Box2(
                cameraTilePos.X - viewWidthTiles / 2 - 1,
                cameraTilePos.Y - viewHeightTiles / 2 - 1,
                cameraTilePos.X + viewWidthTiles / 2 + 1,
                cameraTilePos.Y + viewHeightTiles / 2 + 1
            );
        }

        private void DrawSimpleQuad()
        {
            _spriteRenderer.Begin(Matrix4x4.Identity, Matrix4x4.CreateOrthographicOffCenter(-1, 1, -1, 1, -1, 1));
            _spriteRenderer.Draw(0, new Box2(0, 0, 1, 1), new Vector2(-1, -1), new Vector2(2, 2), Color.White);
            _spriteRenderer.End();
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
            _vulkanContext?.Dispose();
            _spriteRenderer.Dispose();
            _modelRenderer.Dispose();
            _worldRenderer.Dispose();
            _lightingRenderer.Dispose();
            _occluderMap.Dispose();
            _ssaoShader.Dispose();
            _particleSystem.Dispose();
            _bloomShader.Dispose();
            _postProcessShader.Dispose();
            _gBuffer.Dispose();
            _sceneFramebuffer.Dispose();
            _bloomBuffers[0].Dispose();
            _bloomBuffers[1].Dispose();
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
            if (key == Key.O)
            {
                _settingsPanel.IsOpen = !_settingsPanel.IsOpen;
            }
        }
    }
}
