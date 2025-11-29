using Silk.NET.Windowing;
using Silk.NET.Input;
using Silk.NET.OpenGL;
using System;
using Client.Assets;
using Client.Graphics;
using System.Diagnostics;
using Robust.Shared.Maths;
using Core.Dmi;

namespace Client
{
    public class Game
    {
        private IWindow _window;
        private GL _gl;
        private LogicThread _logicThread;
        private AssetManager _assetManager;
        private SpriteRenderer _spriteRenderer;
        private Camera _camera;

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
            _assetManager = new AssetManager(_gl);
            _spriteRenderer = new SpriteRenderer(_gl);
            _camera = new Camera(new Vector2(0, 0), 1.0f);

            _logicThread = new LogicThread();
            _previousState = _logicThread.PreviousState;
            _currentState = _logicThread.CurrentState;
            _logicThread.Start();
        }

        private void OnUpdate(double deltaTime)
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
        }

        private void OnRender(double deltaTime)
        {
            _gl.ClearColor(0.2f, 0.3f, 0.3f, 1.0f);
            _gl.Clear(ClearBufferMask.ColorBufferBit);

            _spriteRenderer.Begin(_camera.GetViewMatrix(), _camera.GetProjectionMatrix((float)_window.FramebufferSize.X / _window.FramebufferSize.Y));

            if (_currentState != null)
            {
                foreach (var currentObj in _currentState.Renderables.Values)
                {
                    Vector2 renderPos;
                    if (_previousState.Renderables.TryGetValue(currentObj.Id, out var previousObj))
                    {
                        renderPos = Vector2.Lerp(previousObj.Position, currentObj.Position, _alpha);
                    }
                    else
                    {
                        renderPos = currentObj.Position;
                    }

                    if (!string.IsNullOrEmpty(currentObj.Icon))
                    {
                        var (dmiPath, stateName) = ParseIconString(currentObj.Icon);
                        if(dmiPath != null)
                        {
                            try
                            {
                                var asset = _assetManager.LoadDmi("assets/" + dmiPath);
                                var state = asset.Description.GetStateOrDefault(stateName);
                                if(state != null)
                                {
                                    var frame = state.GetFrames(AtomDirection.South)[0];
                                    var uv = new Box2(
                                        (float)frame.X / asset.Width,
                                        (float)frame.Y / asset.Height,
                                        (float)(frame.X + asset.Description.Width) / asset.Width,
                                        (float)(frame.Y + asset.Description.Height) / asset.Height
                                    );

                                    _spriteRenderer.Draw(asset.TextureId, uv, renderPos * 32, new Vector2(32, 32), Color.White);
                                }
                            }
                            catch (Exception e)
                            {
                                Console.WriteLine($"Error rendering icon {currentObj.Icon}: {e.Message}");
                                _spriteRenderer.DrawQuad(renderPos * 32, new Vector2(32, 32), Color.Red); // Draw red square on error
                            }
                        }
                    } else {
                         _spriteRenderer.DrawQuad(renderPos * 32, new Vector2(32, 32), Color.White);
                    }
                }
            }

            _spriteRenderer.End();
        }

        private (string?, string?) ParseIconString(string icon)
        {
            var parts = icon.Split(':');
            if (parts.Length == 2)
            {
                return (parts[0], parts[1]);
            }
            if (parts.Length == 1)
            {
                return (parts[0], null);
            }
            return (null, null);
        }

        private void OnClose()
        {
            _logicThread.Stop();
            _spriteRenderer.Dispose();
            _assetManager.Dispose();
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
