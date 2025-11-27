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

        private MainMenuPanel _mainMenuPanel;
        private MenuBarPanel _menuBarPanel = null!;
        private ViewportPanel _viewportPanel = null!;
        private AssetBrowserPanel _assetBrowserPanel = null!;
        private InspectorPanel _inspectorPanel = null!;
        private ObjectBrowserPanel _objectBrowserPanel = null!;
        private ScriptEditorPanel _scriptEditorPanel = null!;
        private SettingsPanel _settingsPanel = null!;
        private ToolboxPanel _toolboxPanel = null!;
        private MapControlsPanel _mapControlsPanel = null!;
        private BuildPanel _buildPanel = null!;

        private BuildService _buildService = null!;

        private AppState _appState = AppState.MainMenu;

        private readonly EditorContext _editorContext;

        public Editor()
        {
            _editorContext = new EditorContext();
            _mainMenuPanel = new MainMenuPanel();
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
            if (_assetManager != null)
            {
                foreach (var path in paths)
                {
                    _assetManager.ImportAsset(path);
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
            var gameApi = new GameApi(_project, gameState, objectTypeManager, mapLoader);

            var wall = new ObjectType("wall");
            wall.DefaultProperties["SpritePath"] = "assets/wall.png";
            objectTypeManager.RegisterObjectType(wall);

            var floor = new ObjectType("floor");
            floor.DefaultProperties["SpritePath"] = "assets/floor.png";
            objectTypeManager.RegisterObjectType(floor);

            toolManager.SetActiveTool(toolManager.Tools.FirstOrDefault(), _editorContext);

            if (gl != null)
            {
                _viewportPanel = new ViewportPanel(gl, gameApi, toolManager, selectionManager, _editorContext);
            }
            _assetBrowserPanel = new AssetBrowserPanel(_assetManager, _project);
            _inspectorPanel = new InspectorPanel(gameApi, selectionManager, _editorContext);
            _objectBrowserPanel = new ObjectBrowserPanel(objectTypeManager, _editorContext);
            _scriptEditorPanel = new ScriptEditorPanel(gameApi);
            _settingsPanel = new SettingsPanel();
            _toolboxPanel = new ToolboxPanel(toolManager, _editorContext);
            _buildService = new BuildService(_project);
            _menuBarPanel = new MenuBarPanel(gameApi, _editorContext, _buildService);
            _mapControlsPanel = new MapControlsPanel(_editorContext);
            _buildPanel = new BuildPanel(_buildService);

            _appState = AppState.Editing;
        }

        private void OnRender(double deltaTime)
        {
            if (imGuiController == null || gl == null) return;

            imGuiController.Update((float)deltaTime);

            gl.ClearColor(0.2f, 0.2f, 0.2f, 1.0f);
            gl.Clear(ClearBufferMask.ColorBufferBit);

            ImGui.DockSpaceOverViewport();

            switch (_appState)
            {
                case AppState.MainMenu:
                    var projectToLoad = _mainMenuPanel.Draw();
                    if (!string.IsNullOrEmpty(projectToLoad))
                    {
                        OnProjectLoad(projectToLoad);
                    }
                    break;
                case AppState.Editing:
                    if (_menuBarPanel != null) _menuBarPanel.Draw();

                    _viewportPanel.Draw();
                    _assetBrowserPanel.Draw();
                    _inspectorPanel.Draw();
                    _objectBrowserPanel.Draw();
                    _scriptEditorPanel.Draw();
                    _toolboxPanel.Draw();
                    _mapControlsPanel.Draw();
                    _buildPanel.Draw();
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
