using Silk.NET.Windowing;
using Silk.NET.OpenGL;
using Silk.NET.Maths;
using ImGuiNET;
using Silk.NET.OpenGL.Extensions.ImGui;
using Silk.NET.Input;
using System.Diagnostics;
using Launcher.UI;
using System;

namespace Launcher
{
    public class Launcher
    {
        private IWindow? _window;
        private GL? _gl;
        private IInputContext? _inputContext;
        private ImGuiController? _imGuiController;
        private MainMenuPanel? _mainMenuPanel;

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

                _mainMenuPanel = new MainMenuPanel();
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

            if (_mainMenuPanel.IsEditorRequested)
            {
                StartEditor();
            }

            if (_mainMenuPanel.IsServerBrowserRequested)
            {
                StartEditor("--panel ServerBrowser");
            }

            _imGuiController.Render();
        }

        private void StartEditor(string? arguments = null)
        {
            if (_window == null) return;

            try
            {
                var startInfo = new ProcessStartInfo
                {
                    FileName = "Editor.exe",
                    Arguments = arguments ?? string.Empty
                };
                Process.Start(startInfo);
                _window.Close();
            }
            catch (Exception e)
            {
                // Handle case where Editor.exe is not found
                Console.WriteLine($"Error starting Editor: {e.Message}");
                // Optionally, show an error message in the UI
            }
        }

        private void OnClose()
        {
            _imGuiController?.Dispose();
            _gl?.Dispose();
        }
    }
}
