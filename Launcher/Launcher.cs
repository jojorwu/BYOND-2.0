using Silk.NET.Windowing;
using Silk.NET.OpenGL;
using Silk.NET.Maths;
using ImGuiNET;
using Silk.NET.OpenGL.Extensions.ImGui;
using Silk.NET.Input;
using Launcher.UI;
using System;
using System.IO;
using Launcher.Services;

namespace Launcher
{
    public class Launcher : IDisposable
    {
        private IWindow? _window;
        private GL? _gl;
        private IInputContext? _inputContext;
        private ImGuiController? _imGuiController;
        private MainMenuPanel? _mainMenuPanel;
        private Texture? _logoTexture;

        public void Run()
        {
            var options = WindowOptions.Default;
            options.Title = "BYOND 2.0 Launcher";
            options.Size = new Vector2D<int>(640, 480);
            options.WindowBorder = WindowBorder.Fixed; // Make window non-resizable

            _window = Window.Create(options);

            _window.Load += OnLoad;
            _window.Render += OnRender;
            _window.Closing += OnClose;

            _window.Run();
        }

        private void OnLoad()
        {
            if (_window != null)
            {
                _gl = _window.CreateOpenGL();
                _inputContext = _window.CreateInput();
                _imGuiController = new ImGuiController(_gl, _window, _inputContext);

                try
                {
                    _logoTexture = new Texture(_gl, Path.Combine("assets", "logo.png"));
                }
                catch (FileNotFoundException)
                {
                    Console.WriteLine("Logo file not found. Continuing without logo.");
                    _logoTexture = null;
                }


                _mainMenuPanel = new MainMenuPanel(_logoTexture, new ProcessService(message => _mainMenuPanel?.ShowError(message)));
            }
        }

        private void OnRender(double deltaTime)
        {
            if (_imGuiController == null || _gl == null || _window == null || _mainMenuPanel == null) return;

            _imGuiController.Update((float)deltaTime);

            _gl.ClearColor(0.1f, 0.1f, 0.1f, 1.0f);
            _gl.Clear(ClearBufferMask.ColorBufferBit);

            _mainMenuPanel.Draw();

            if (_mainMenuPanel.IsExitRequested)
            {
                _window.Close();
            }

            _imGuiController.Render();
        }

        private void OnClose()
        {
            Dispose();
        }

        public void Dispose()
        {
            _imGuiController?.Dispose();
            _logoTexture?.Dispose();
            _gl?.Dispose();
        }
    }
}
