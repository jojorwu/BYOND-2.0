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
using System.Collections.Generic;

namespace Editor
{
    public class Editor
    {
        private IWindow? window;
        public GL? gl { get; private set; }
        private IInputContext? inputContext;
        private ImGuiController? imGuiController;

        private readonly IServiceProvider _serviceProvider;
        private readonly IEnumerable<IUiPanel> _panels;
        private readonly TextureManager _textureManager;
        private readonly ViewportPanel _viewportPanel;

        public Editor(IServiceProvider serviceProvider, IEnumerable<IUiPanel> panels, TextureManager textureManager)
        {
            _serviceProvider = serviceProvider;
            _panels = panels;
            _textureManager = textureManager;
            _viewportPanel = panels.OfType<ViewportPanel>().FirstOrDefault()!;
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
            }
        }

        private void OnRender(double deltaTime)
        {
            if (imGuiController == null || gl == null) return;

            imGuiController.Update((float)deltaTime);

            gl.ClearColor(0.2f, 0.2f, 0.2f, 1.0f);
            gl.Clear(ClearBufferMask.ColorBufferBit);

            foreach (var panel in _panels)
            {
                panel.Draw();
            }

            var menuBarPanel = _panels.OfType<MenuBarPanel>().FirstOrDefault();
            if (menuBarPanel?.IsExitRequested == true)
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
