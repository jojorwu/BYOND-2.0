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
using AssetManager = Core.AssetManager;
using Client.UI;
using ImGuiNET;
using Silk.NET.OpenGL.Extensions.ImGui;

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
        private AssetManager _assetManager;
        private SpriteRenderer _spriteRenderer;
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
            _assetManager = new AssetManager();
            _spriteRenderer = new SpriteRenderer(_gl);
            _camera = new Camera(new Vector2(0, 0), 1.0f);
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

        private void OnRender(double deltaTime)
        {
            _gl.ClearColor(0.2f, 0.3f, 0.3f, 1.0f);
            _gl.Clear(ClearBufferMask.ColorBufferBit);

            if (_clientState == ClientState.Connecting)
            {
                _connectionPanel.Draw();
            }
            else if (_clientState == ClientState.InGame)
            {
                _spriteRenderer.Begin(_camera.GetViewMatrix(), _camera.GetProjectionMatrix((float)_window.FramebufferSize.X / _window.FramebufferSize.Y));

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

                                        _spriteRenderer.Draw(dmi.TextureId, uv, renderPos * 32, new Vector2(32, 32), Color.White);
                                    }
                                }
                                catch (Exception e)
                                {
                                    Console.WriteLine($"Error rendering icon {icon}: {e.Message}");
                                    _spriteRenderer.DrawQuad(renderPos * 32, new Vector2(32, 32), Color.Red); // Draw red square on error
                                }
                            } else {
                                _spriteRenderer.DrawQuad(renderPos * 32, new Vector2(32, 32), Color.Magenta); // Draw magenta square for missing dmi
                            }
                        } else {
                             _spriteRenderer.DrawQuad(renderPos * 32, new Vector2(32, 32), Color.White);
                        }
                    }
                }

                _spriteRenderer.End();
            }

            _imGuiController.Render();
        }

        private Vector2 GetPosition(GameObject obj)
        {
            if (obj.Properties.TryGetValue("Position", out var pos) && pos is JsonElement posElement)
            {
                return new Vector2(posElement.GetProperty("X").GetSingle(), posElement.GetProperty("Y").GetSingle());
            }
            return Vector2.Zero;
        }

        private string? GetIcon(GameObject obj)
        {
            if (obj.Properties.TryGetValue("Icon", out var icon) && icon is string iconStr)
            {
                return iconStr;
            }
            return null;
        }

        private void OnClose()
        {
            _logicThread?.Stop();
            _spriteRenderer.Dispose();
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
