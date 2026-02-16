using Shared;
using Silk.NET.Windowing;
using Silk.NET.OpenGL;
using Silk.NET.Maths;
using Editor.UI;
using ImGuiNET;
using Core;
using Silk.NET.OpenGL.Extensions.ImGui;
using Silk.NET.Input;
using System.Linq;
using Microsoft.Extensions.DependencyInjection;
using System;

namespace Editor
{
    public class Editor
    {
        private IWindow? window;
        public GL? gl { get; private set; }
        private IInputContext? inputContext;
        private ImGuiController? imGuiController;

        private readonly IServiceProvider _serviceProvider;
        private readonly MainPanel _mainPanel;
        private readonly MenuBarPanel _menuBarPanel; // Keep for exit request
        private readonly ViewportPanel _viewportPanel; // Keep for GL initialization
        private readonly TextureManager _textureManager;
        private readonly IProjectService _projectService;
        private readonly SettingsPanel _settingsPanel;
        private readonly IRunService _runService;
        private readonly IEditorSettingsManager _settingsManager;

        private bool _lastThemeWasDark = true;

        public Editor(IServiceProvider serviceProvider,
            MainPanel mainPanel, MenuBarPanel menuBarPanel, ViewportPanel viewportPanel,
            TextureManager textureManager, IProjectService projectService, SettingsPanel settingsPanel,
            IRunService runService, IEditorSettingsManager settingsManager)
        {
            _serviceProvider = serviceProvider;
            _mainPanel = mainPanel;
            _menuBarPanel = menuBarPanel;
            _viewportPanel = viewportPanel;
            _textureManager = textureManager;
            _projectService = projectService;
            _settingsPanel = settingsPanel;
            _runService = runService;
            _settingsManager = settingsManager;
        }

        public void Run()
        {
            var options = WindowOptions.Default;
            options.Title = "BYOND 2.0 Editor";
            options.Size = new Vector2D<int>(1280, 720);

            window = Window.Create(options);

            window.Load += OnLoad;
            window.Render += OnRender;
            window.Closing += OnClose;
            window.FileDrop += OnFileDrop;

            window.Run();
        }

        private void OnFileDrop(string[] paths)
        {
            // This logic might need to be moved depending on which tab is active.
            // For now, we assume it's for the scene editor.
            var editorContext = _serviceProvider.GetRequiredService<EditorContext>();
             if (string.IsNullOrEmpty(editorContext.ProjectRoot)) return;

            foreach (var path in paths)
            {
                var extension = System.IO.Path.GetExtension(path).ToLowerInvariant();
                var fileName = System.IO.Path.GetFileName(path);
                string destDir = extension switch
                {
                    ".dmm" or ".json" => "maps",
                    ".dm" => "code",
                    ".png" or ".jpg" or ".jpeg" or ".gif" or ".bmp" => "assets",
                    _ => ""
                };

                if (!string.IsNullOrEmpty(destDir))
                {
                    var destPath = System.IO.Path.Combine(editorContext.ProjectRoot, destDir, fileName);
                    System.IO.File.Copy(path, destPath, true);
                    Console.WriteLine($"Imported '{fileName}' to '{destDir}'");
                }
            }
        }

        private void OnLoad()
        {
            if (window != null)
            {
                gl = window.CreateOpenGL();
                inputContext = window.CreateInput();
                imGuiController = new ImGuiController(gl, window, inputContext);

                _textureManager.Initialize(gl);
                _viewportPanel.Initialize(gl);

                ImGui.GetIO().ConfigFlags |= ImGuiConfigFlags.DockingEnable;
                ApplyModernStyle();
            }
        }

        private void ApplyModernStyle()
        {
            var style = ImGui.GetStyle();
            style.WindowRounding = 5.0f;
            style.FrameRounding = 4.0f;
            style.PopupRounding = 4.0f;
            style.GrabRounding = 4.0f;
            style.TabRounding = 4.0f;
            style.ScrollbarRounding = 9.0f;

            style.WindowPadding = new System.Numerics.Vector2(10, 10);
            style.FramePadding = new System.Numerics.Vector2(5, 5);
            style.ItemSpacing = new System.Numerics.Vector2(8, 8);

            var colors = style.Colors;
            colors[(int)ImGuiCol.WindowBg] = new System.Numerics.Vector4(0.12f, 0.12f, 0.12f, 1.00f);
            colors[(int)ImGuiCol.Header] = new System.Numerics.Vector4(0.20f, 0.20f, 0.20f, 1.00f);
            colors[(int)ImGuiCol.HeaderHovered] = new System.Numerics.Vector4(0.25f, 0.25f, 0.25f, 1.00f);
            colors[(int)ImGuiCol.HeaderActive] = new System.Numerics.Vector4(0.30f, 0.30f, 0.30f, 1.00f);
            colors[(int)ImGuiCol.Button] = new System.Numerics.Vector4(0.20f, 0.20f, 0.20f, 1.00f);
            colors[(int)ImGuiCol.ButtonHovered] = new System.Numerics.Vector4(0.28f, 0.28f, 0.28f, 1.00f);
            colors[(int)ImGuiCol.ButtonActive] = new System.Numerics.Vector4(0.06f, 0.53f, 0.98f, 1.00f);
            colors[(int)ImGuiCol.FrameBg] = new System.Numerics.Vector4(0.20f, 0.20f, 0.20f, 1.00f);
            colors[(int)ImGuiCol.FrameBgHovered] = new System.Numerics.Vector4(0.25f, 0.25f, 0.25f, 1.00f);
            colors[(int)ImGuiCol.FrameBgActive] = new System.Numerics.Vector4(0.30f, 0.30f, 0.30f, 1.00f);
            colors[(int)ImGuiCol.Tab] = new System.Numerics.Vector4(0.15f, 0.15f, 0.15f, 1.00f);
            colors[(int)ImGuiCol.TabHovered] = new System.Numerics.Vector4(0.28f, 0.28f, 0.28f, 1.00f);
            // TabActive might be TabActive in some versions
            colors[(int)ImGuiCol.TitleBg] = new System.Numerics.Vector4(0.10f, 0.10f, 0.10f, 1.00f);
            colors[(int)ImGuiCol.TitleBgActive] = new System.Numerics.Vector4(0.15f, 0.15f, 0.15f, 1.00f);
        }

        private void OnRender(double deltaTime)
        {
            if (imGuiController == null || gl == null) return;

            var settings = _settingsManager.Settings;
            if (settings.UseDarkTheme != _lastThemeWasDark)
            {
                if (settings.UseDarkTheme) ImGui.StyleColorsDark();
                else ImGui.StyleColorsLight();
                _lastThemeWasDark = settings.UseDarkTheme;
            }

            imGuiController.Update((float)deltaTime);

            gl.ClearColor(0.1f, 0.1f, 0.1f, 1.0f);
            gl.Clear(ClearBufferMask.ColorBufferBit);

            _mainPanel.Draw();
            _settingsPanel.Draw();
            _runService.Draw();

            if (_menuBarPanel.IsExitRequested)
            {
                window?.Close();
            }

            imGuiController?.Render();
        }

        private void OnClose()
        {
            imGuiController?.Dispose();
            _viewportPanel?.Dispose();
            gl?.Dispose();
        }
    }
}
