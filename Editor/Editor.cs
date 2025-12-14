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
        private readonly ProjectHolder _projectHolder;
        private readonly MainPanel _mainPanel;
        private readonly MenuBarPanel _menuBarPanel; // Keep for exit request
        private readonly ViewportPanel _viewportPanel; // Keep for GL initialization
        private readonly TextureManager _textureManager;

        public Editor(IServiceProvider serviceProvider, ProjectHolder projectHolder,
            MainPanel mainPanel, MenuBarPanel menuBarPanel, ViewportPanel viewportPanel, TextureManager textureManager)
        {
            _serviceProvider = serviceProvider;
            _projectHolder = projectHolder;
            _mainPanel = mainPanel;
            _menuBarPanel = menuBarPanel;
            _viewportPanel = viewportPanel;
            _textureManager = textureManager;
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

        public void LoadProject(string projectPath)
        {
            var project = new Project(projectPath);
            _projectHolder.SetProject(project);

            var objectTypeManager = _serviceProvider.GetRequiredService<IObjectTypeManager>();
            var wall = new ObjectType(1, "wall");
            wall.DefaultProperties["SpritePath"] = "assets/wall.png";
            objectTypeManager.RegisterObjectType(wall);
            var floor = new ObjectType(2, "floor");
            floor.DefaultProperties["SpritePath"] = "assets/floor.png";
            objectTypeManager.RegisterObjectType(floor);

            var toolManager = _serviceProvider.GetRequiredService<ToolManager>();
            var editorContext = _serviceProvider.GetRequiredService<EditorContext>();
            toolManager.SetActiveTool(toolManager.Tools.FirstOrDefault(), editorContext);

            // TODO: Tell MainPanel to switch to the Scene tab.
        }

        private void OnRender(double deltaTime)
        {
            if (imGuiController == null || gl == null) return;

            imGuiController.Update((float)deltaTime);

            gl.ClearColor(0.2f, 0.2f, 0.2f, 1.0f);
            gl.Clear(ClearBufferMask.ColorBufferBit);

            _mainPanel.Draw();

            if (_menuBarPanel.IsExitRequested)
            {
                window?.Close();
            }

            // Handle project loading requests from UI panels
            if (!string.IsNullOrEmpty(_menuBarPanel.ProjectToLoad))
            {
                LoadProject(_menuBarPanel.ProjectToLoad);
            }
            // A similar check will be needed for the new ProjectsPanel

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
