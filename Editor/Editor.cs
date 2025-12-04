using Silk.NET.Windowing;
using Silk.NET.OpenGL;
using Silk.NET.Maths;
using Core;
using ImGuiNET;
using Silk.NET.OpenGL.Extensions.ImGui;
using Silk.NET.Input;
using Editor.UI;
using System.Linq;

namespace Editor
{
    public class Editor
    {
        private IWindow? window;
        private GL? gl;
        private IInputContext? inputContext;
        private ImGuiController? imGuiController;

        private Project? _project;
        private AssetManager? _assetManager;
        private OpenDreamCompilerService? _compilerService;
        private DmmService? _dmmService;
        private TextureManager? _textureManager;

        private ProjectManagerPanel _projectManagerPanel;
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

        private BuildService _buildService = null!;

        private AppState _appState = AppState.MainMenu;
        private bool _firstTime = true;
        private int _sceneToClose = -1;

        private readonly EditorContext _editorContext;
        private readonly LocalizationManager _localizationManager;

        public Editor()
        {
            _editorContext = new EditorContext();
            _localizationManager = new LocalizationManager();
            _localizationManager.LoadLanguage("en");
            _projectManagerPanel = new ProjectManagerPanel(_editorContext, _localizationManager);
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
                    var destPath = System.IO.Path.Combine(_editorContext.ProjectRoot, destDir, fileName);
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
            }
        }

        private void OnProjectLoad(string projectPath)
        {
            _project = new Project(projectPath);
            var gameState = new GameState();
            var objectTypeManager = new ObjectTypeManager();
            var mapLoader = new MapLoader(objectTypeManager);
            _assetManager = new AssetManager();
            var selectionManager = new SelectionManager();
            var toolManager = new ToolManager();
            var mapApi = new MapApi(gameState, mapLoader, _project);
            var objectApi = new ObjectApi(gameState, objectTypeManager, mapApi);
            var scriptApi = new ScriptApi(_project);
            var standardLibraryApi = new StandardLibraryApi(gameState, objectTypeManager, mapApi);
            var gameApi = new GameApi(mapApi, objectApi, scriptApi, standardLibraryApi);
            _compilerService = new OpenDreamCompilerService(_project);
            _dmmService = new DmmService(objectTypeManager, _project);

            var wall = new ObjectType("wall");
            wall.DefaultProperties["SpritePath"] = "assets/wall.png";
            objectTypeManager.RegisterObjectType(wall);

            var floor = new ObjectType("floor");
            floor.DefaultProperties["SpritePath"] = "assets/floor.png";
            objectTypeManager.RegisterObjectType(floor);

            toolManager.SetActiveTool(toolManager.Tools.FirstOrDefault(), _editorContext);

            if (gl != null)
            {
                _textureManager = new TextureManager(gl);
                _viewportPanel = new ViewportPanel(gl, toolManager, selectionManager, _editorContext, gameApi);
                _assetBrowserPanel = new AssetBrowserPanel(_project, _editorContext, _textureManager, _localizationManager);
                _inspectorPanel = new InspectorPanel(gameApi, selectionManager, _editorContext, _assetBrowserPanel, gl);
            }
            _objectBrowserPanel = new ObjectBrowserPanel(objectTypeManager, _editorContext);
            _scriptEditorPanel = new ScriptEditorPanel();
            _settingsPanel = new SettingsPanel(_localizationManager);
            _toolbarPanel = new ToolbarPanel(_editorContext, toolManager);
            _buildService = new BuildService(_project);
            _menuBarPanel = new MenuBarPanel(gameApi, _editorContext, _buildService, _dmmService, _localizationManager);
            _mapControlsPanel = new MapControlsPanel(_editorContext);
            _buildPanel = new BuildPanel(_buildService);
            _sceneHierarchyPanel = new SceneHierarchyPanel(gameApi, selectionManager);

            _appState = AppState.Editing;
        }

        private void OnRender(double deltaTime)
        {
            if (imGuiController == null || gl == null) return;

            imGuiController.Update((float)deltaTime);

            gl.ClearColor(0.2f, 0.2f, 0.2f, 1.0f);
            gl.Clear(ClearBufferMask.ColorBufferBit);

            ImGui.DockSpaceOverViewport(ImGui.GetMainViewport(), ImGuiDockNodeFlags.PassthruCentralNode);

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
                    if (_firstTime)
                    {
                        var dockSpaceId = ImGui.GetID("MyDockSpace");
                        ImGui.DockBuilderRemoveNode(dockSpaceId);
                        ImGui.DockBuilderAddNode(dockSpaceId, ImGuiDockNodeFlags.DockSpace);
                        ImGui.DockBuilderSetNodeSize(dockSpaceId, ImGui.GetMainViewport().Size);

                        var dockMainId = dockSpaceId;
                        var dockRightId = ImGui.DockBuilderSplitNode(dockMainId, ImGuiDir.Right, 0.2f, out _, out dockMainId);
                        var dockLeftId = ImGui.DockBuilderSplitNode(dockMainId, ImGuiDir.Left, 0.2f, out _, out dockMainId);
                        var dockBottomId = ImGui.DockBuilderSplitNode(dockMainId, ImGuiDir.Down, 0.25f, out _, out dockMainId);

                        ImGui.DockBuilderDockWindow("Scene Hierarchy", dockLeftId);
                        ImGui.DockBuilderDockWindow("Assets", dockLeftId);
                        ImGui.DockBuilderDockWindow("Object Types", dockLeftId);
                        ImGui.DockBuilderDockWindow("Inspector", dockRightId);
                        ImGui.DockBuilderDockWindow("Build", dockBottomId);
                        ImGui.DockBuilderDockWindow("MainView", dockMainId);
                        // Toolbar is not a dockable window, it will be drawn above the dock space.

                        ImGui.DockBuilderFinish(dockSpaceId);
                        _firstTime = false;
                    }

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
                    if (ImGui.BeginTabBar("SceneTabs"))
                    {
                        for (int i = 0; i < _editorContext.OpenScenes.Count; i++)
                        {
                            var scene = _editorContext.OpenScenes[i];
                            bool isOpen = true;
                            var tabName = System.IO.Path.GetFileName(scene.FilePath);
                            if (scene.IsDirty) tabName += "*";
                            if (ImGui.BeginTabItem(tabName, ref isOpen))
                            {
                                _editorContext.ActiveSceneIndex = i;
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
                                    _editorContext.OpenScenes.RemoveAt(i);
                                    if (_editorContext.ActiveSceneIndex >= i)
                                    {
                                        _editorContext.ActiveSceneIndex--;
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
                            var scene = _editorContext.OpenScenes[_sceneToClose];
                            _menuBarPanel.SaveScene(scene, false);
                            _editorContext.OpenScenes.RemoveAt(_sceneToClose);
                            if (_editorContext.ActiveSceneIndex >= _sceneToClose)
                            {
                                _editorContext.ActiveSceneIndex--;
                            }
                            ImGui.CloseCurrentPopup();
                        }
                        ImGui.SameLine();
                        if (ImGui.Button("Don't Save"))
                        {
                            _editorContext.OpenScenes.RemoveAt(_sceneToClose);
                            if (_editorContext.ActiveSceneIndex >= _sceneToClose)
                            {
                                _editorContext.ActiveSceneIndex--;
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
