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

        private IGame _game = null!;
        private IServiceProvider _serviceProvider = null!;

        private ProjectManagerPanel _projectManagerPanel = null!;
        private MenuBarPanel _menuBarPanel = null!;
        private ViewportPanel _viewportPanel = null!;
        private AssetBrowserPanel _assetBrowserPanel = null!;
        private InspectorPanel _inspectorPanel = null!;
        private ObjectBrowserPanel _objectBrowserPanel = null!;
        private ScriptEditorPanel _scriptEditorPanel = null!;
        private SettingsPanel _settingsPanel = null!;
        private ToolbarPanel _toolbarPanel = null!;
        private MapControlsPanel _mapControlsPanel = null!;
        private BuildPanel _buildPanel = null!;
        private SceneHierarchyPanel _sceneHierarchyPanel = null!;

        private AppState _appState = AppState.MainMenu;
        private int _sceneToClose = -1;

        public Editor()
        {
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
                ImGui.GetIO().ConfigFlags |= ImGuiConfigFlags.DockingEnable;
            }
        }

        private void OnProjectLoad(string projectPath)
        {
            _game = GameFactory.CreateGame();
            _game.LoadProject(projectPath);

            var services = new ServiceCollection();
            ConfigureServices(services, _game, gl!);
            _serviceProvider = services.BuildServiceProvider();

            _projectManagerPanel = _serviceProvider.GetRequiredService<ProjectManagerPanel>();
            _menuBarPanel = _serviceProvider.GetRequiredService<MenuBarPanel>();
            _viewportPanel = _serviceProvider.GetRequiredService<ViewportPanel>();
            _assetBrowserPanel = _serviceProvider.GetRequiredService<AssetBrowserPanel>();
            _inspectorPanel = _serviceProvider.GetRequiredService<InspectorPanel>();
            _objectBrowserPanel = _serviceProvider.GetRequiredService<ObjectBrowserPanel>();
            _scriptEditorPanel = _serviceProvider.GetRequiredService<ScriptEditorPanel>();
            _settingsPanel = _serviceProvider.GetRequiredService<SettingsPanel>();
            _toolbarPanel = _serviceProvider.GetRequiredService<ToolbarPanel>();
            _mapControlsPanel = _serviceProvider.GetRequiredService<MapControlsPanel>();
            _buildPanel = _serviceProvider.GetRequiredService<BuildPanel>();
            _sceneHierarchyPanel = _serviceProvider.GetRequiredService<SceneHierarchyPanel>();

            var toolManager = _serviceProvider.GetRequiredService<ToolManager>();
            var editorContext = _serviceProvider.GetRequiredService<EditorContext>();
            toolManager.SetActiveTool(toolManager.Tools.FirstOrDefault(), editorContext);

            _appState = AppState.Editing;
        }

        private void ConfigureServices(IServiceCollection services, IGame game, GL gl)
        {
            services.AddSingleton(game.Api);
            services.AddSingleton(game.Project);
            services.AddSingleton(game.ObjectTypeManager);
            services.AddSingleton(game.DmmService);
            services.AddSingleton(gl);
            services.AddSingleton<EditorContext>();
            services.AddSingleton<LocalizationManager>(provider =>
            {
                var lm = new LocalizationManager();
                lm.LoadLanguage("en");
                return lm;
            });
            services.AddSingleton<TextureManager>();
            services.AddSingleton<AssetManager>();
            services.AddSingleton<SelectionManager>();
            services.AddSingleton<ToolManager>();
            services.AddSingleton<BuildService>();

            services.AddSingleton<ProjectManagerPanel>();
            services.AddSingleton<MenuBarPanel>();
            services.AddSingleton<ViewportPanel>();
            services.AddSingleton<AssetBrowserPanel>();
            services.AddSingleton<InspectorPanel>();
            services.AddSingleton<ObjectBrowserPanel>();
            services.AddSingleton<ScriptEditorPanel>();
            services.AddSingleton<SettingsPanel>();
            services.AddSingleton<ToolbarPanel>();
            services.AddSingleton<MapControlsPanel>();
            services.AddSingleton<BuildPanel>();
            services.AddSingleton<SceneHierarchyPanel>();
            services.AddSingleton<ServerBrowserPanel>();
            services.AddSingleton<SpriteRenderer>();
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
                    var localizationManager = new LocalizationManager();
                    localizationManager.LoadLanguage("en");
                    _projectManagerPanel = new ProjectManagerPanel(new EditorContext(),localizationManager, new ServerBrowserPanel(localizationManager));
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
