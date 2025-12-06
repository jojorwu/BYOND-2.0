using Silk.NET.Windowing;
using Silk.NET.OpenGL;
using Silk.NET.Maths;
using Core;
using Editor.UI;
using ImGuiNET;
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

        private readonly IGame _game;
        private readonly IServiceProvider _serviceProvider;
        private readonly ProjectHolder _projectHolder;

        private readonly ProjectManagerPanel _projectManagerPanel;
        private readonly MenuBarPanel _menuBarPanel;
        private readonly ViewportPanel _viewportPanel;
        private readonly AssetBrowserPanel _assetBrowserPanel;
        private readonly InspectorPanel _inspectorPanel;
        private readonly ObjectBrowserPanel _objectBrowserPanel;
        private readonly ScriptEditorPanel _scriptEditorPanel;
        private readonly SettingsPanel _settingsPanel;
        private readonly ToolbarPanel _toolbarPanel;
        private readonly MapControlsPanel _mapControlsPanel;
        private readonly BuildPanel _buildPanel;
        private readonly SceneHierarchyPanel _sceneHierarchyPanel;
        private readonly TextureManager _textureManager;

        private AppState _appState = AppState.MainMenu;
        private int _sceneToClose = -1;

        public Editor(IServiceProvider serviceProvider, IGame game, ProjectHolder projectHolder,
            ProjectManagerPanel projectManagerPanel, MenuBarPanel menuBarPanel, ViewportPanel viewportPanel,
            AssetBrowserPanel assetBrowserPanel, InspectorPanel inspectorPanel, ObjectBrowserPanel objectBrowserPanel,
            ScriptEditorPanel scriptEditorPanel, SettingsPanel settingsPanel, ToolbarPanel toolbarPanel,
            MapControlsPanel mapControlsPanel, BuildPanel buildPanel, SceneHierarchyPanel sceneHierarchyPanel,
            TextureManager textureManager)
        {
            _serviceProvider = serviceProvider;
            _game = game;
            _textureManager = textureManager;
            _projectHolder = projectHolder;
            _projectManagerPanel = projectManagerPanel;
            _menuBarPanel = menuBarPanel;
            _viewportPanel = viewportPanel;
            _assetBrowserPanel = assetBrowserPanel;
            _inspectorPanel = inspectorPanel;
            _objectBrowserPanel = objectBrowserPanel;
            _scriptEditorPanel = scriptEditorPanel;
            _settingsPanel = settingsPanel;
            _toolbarPanel = toolbarPanel;
            _mapControlsPanel = mapControlsPanel;
            _buildPanel = buildPanel;
            _sceneHierarchyPanel = sceneHierarchyPanel;
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
            if (_appState != AppState.Editing) return;

            var editorContext = _serviceProvider.GetRequiredService<EditorContext>();
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

        private void OnProjectLoad(string projectPath)
        {
            _projectHolder.SetProject(new Project(projectPath));
            _game.LoadProject(projectPath);

            var toolManager = _serviceProvider.GetRequiredService<ToolManager>();
            var editorContext = _serviceProvider.GetRequiredService<EditorContext>();
            toolManager.SetActiveTool(toolManager.Tools.FirstOrDefault(), editorContext);

            _appState = AppState.Editing;
        }

        private void OnRender(double deltaTime)
        {
            if (imGuiController == null || gl == null) return;

            imGuiController.Update((float)deltaTime);

            gl.ClearColor(0.2f, 0.2f, 0.2f, 1.0f);
            gl.Clear(ClearBufferMask.ColorBufferBit);

            switch (_appState)
            {
                case AppState.MainMenu:
                    var projectToLoad = _projectManagerPanel.Draw();
                    if (!string.IsNullOrEmpty(projectToLoad))
                    {
                        OnProjectLoad(projectToLoad);
                    }
                    break;
                case AppState.Editing:
                    ImGui.DockSpace(ImGui.GetID("MyDockSpace"));

                    _menuBarPanel.Draw();
                    if (_menuBarPanel.IsExitRequested)
                    {
                        window?.Close();
                    }
                    if (!string.IsNullOrEmpty(_menuBarPanel.ProjectToLoad))
                    {
                        OnProjectLoad(_menuBarPanel.ProjectToLoad);
                    }
                    _toolbarPanel.Draw();
                    _assetBrowserPanel.Draw();
                    _inspectorPanel.Draw();
                    _objectBrowserPanel.Draw();
                    _sceneHierarchyPanel.Draw();
                    _mapControlsPanel.Draw();
                    _buildPanel.Draw();

                    ImGui.Begin("MainView");
                    var editorContext = _serviceProvider.GetRequiredService<EditorContext>();
                    if (ImGui.BeginTabBar("SceneTabs"))
                    {
                        for (int i = 0; i < editorContext.OpenScenes.Count; i++)
                        {
                            var scene = editorContext.OpenScenes[i];
                            bool isOpen = true;
                            var tabName = System.IO.Path.GetFileName(scene.FilePath);
                            if (scene.IsDirty) tabName += "*";
                            if (ImGui.BeginTabItem(tabName, ref isOpen))
                            {
                                editorContext.ActiveSceneIndex = i;
                                _viewportPanel.Draw(scene);
                                ImGui.EndTabItem();
                            }

                            if (!isOpen)
                            {
                                if (scene.IsDirty)
                                {
                                    _sceneToClose = i;
                                    ImGui.OpenPopup("Save Changes?");
                                }
                                else
                                {
                                    editorContext.OpenScenes.RemoveAt(i);
                                    if (editorContext.ActiveSceneIndex >= i)
                                    {
                                        editorContext.ActiveSceneIndex--;
                                    }
                                }
                            }
                        }
                        ImGui.EndTabBar();
                    }

                    if (ImGui.BeginPopupModal("Save Changes?"))
                    {
                        ImGui.Text("You have unsaved changes. Save before closing?");
                        if (ImGui.Button("Save"))
                        {
                            var scene = editorContext.OpenScenes[_sceneToClose];
                            _menuBarPanel.SaveScene(scene, false);
                            editorContext.OpenScenes.RemoveAt(_sceneToClose);
                            if (editorContext.ActiveSceneIndex >= _sceneToClose)
                            {
                                editorContext.ActiveSceneIndex--;
                            }
                            ImGui.CloseCurrentPopup();
                        }
                        ImGui.SameLine();
                        if (ImGui.Button("Don't Save"))
                        {
                            editorContext.OpenScenes.RemoveAt(_sceneToClose);
                            if (editorContext.ActiveSceneIndex >= _sceneToClose)
                            {
                                editorContext.ActiveSceneIndex--;
                            }
                            ImGui.CloseCurrentPopup();
                        }
                        ImGui.SameLine();
                        if (ImGui.Button("Cancel"))
                        {
                            ImGui.CloseCurrentPopup();
                        }
                        ImGui.EndPopup();
                    }
                    ImGui.End();
                    break;
                case AppState.Settings:
                    _settingsPanel.Draw();
                    break;
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

    public enum AppState
    {
        MainMenu,
        Editing,
        Settings
    }
}
