using Silk.NET.Windowing;
using Silk.NET.OpenGL;
using Silk.NET.Maths;
using ImGuiNET;
using Silk.NET.OpenGL.Extensions.ImGui;
using Silk.NET.Input;
using System.Diagnostics;
using Launcher.UI;
using System;
using System.ComponentModel;
using System.IO;
using Shared.Interfaces;
using Shared.Messaging;

namespace Launcher
{
    public class Launcher : IDisposable
    {
        private readonly IEngineManager _engineManager;
        private readonly IEventBus _eventBus;
        private readonly IComputeService _computeService;
        private IWindow? _window;
        private GL? _gl;
        private IInputContext? _inputContext;
        private ImGuiController? _imGuiController;
        private MainMenuPanel? _mainMenuPanel;
        private Texture? _logoTexture;

        public Launcher(IEngineManager engineManager, IEventBus eventBus, IComputeService computeService)
        {
            _engineManager = engineManager;
            _eventBus = eventBus;
            _computeService = computeService;
        }

        public void Run()
        {
            if (_computeService is IAsyncInitializable initializable)
            {
                _ = initializable.InitializeAsync();
            }

            var options = WindowOptions.Default;
            options.Title = "BYOND 2.0 Developer Launcher";
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

                UiTheme.Apply();

                try
                {
                    _logoTexture = new Texture(_gl, Path.Combine("assets", "logo.png"));
                }
                catch (FileNotFoundException)
                {
                    Console.WriteLine("Logo file not found. Continuing without logo.");
                    _logoTexture = null;
                }

                _mainMenuPanel = new MainMenuPanel(_logoTexture, _engineManager, _computeService);
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
                _mainMenuPanel.IsEditorRequested = false; // Reset flag
                StartComponent(EngineComponent.Editor);
            }

            if (_mainMenuPanel.IsServerBrowserRequested)
            {
                _mainMenuPanel.IsServerBrowserRequested = false; // Reset flag
                StartComponent(EngineComponent.Editor, "--panel ServerBrowser");
            }

            if (_mainMenuPanel.IsServerRequested)
            {
                _mainMenuPanel.IsServerRequested = false; // Reset flag
                StartComponent(EngineComponent.Server);
            }

            if (_mainMenuPanel.IsClientRequested)
            {
                _mainMenuPanel.IsClientRequested = false; // Reset flag
                StartComponent(EngineComponent.Client);
            }

            if (_mainMenuPanel.IsCompileRequested)
            {
                _mainMenuPanel.IsCompileRequested = false; // Reset flag
                CompileAndRun();
            }

            _imGuiController.Render();
        }

        private void CompileAndRun()
        {
            StartComponent(EngineComponent.Compiler, "Project.dm");
            StartComponent(EngineComponent.Server);
        }

        private void StartComponent(EngineComponent component, string? arguments = null)
        {
            if (_window == null || _mainMenuPanel == null) return;

            string fileName = _engineManager.GetExecutablePath(component);

            if (!_engineManager.IsComponentInstalled(component))
            {
                _mainMenuPanel.ShowError($"{component} is not installed.\n\nPath: {fileName}");
                return;
            }

            try
            {
                var startInfo = new ProcessStartInfo
                {
                    FileName = fileName,
                    Arguments = arguments ?? string.Empty,
#if DEBUG
                    UseShellExecute = false,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
#else
                    UseShellExecute = true,
#endif
                };
                Process.Start(startInfo);
            }
            catch (Win32Exception e)
            {
                _mainMenuPanel.ShowError($"Error starting {component}:\n{e.Message}\n\nPath: {fileName}");
            }
            catch (Exception e)
            {
                _mainMenuPanel.ShowError($"An unexpected error occurred:\n{e.Message}");
            }
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
