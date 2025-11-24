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
        private TextureManager? _textureManager;
        private SpriteRenderer? _spriteRenderer;

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
            gl = window.CreateOpenGL();
            inputContext = window.CreateInput();
            imGuiController = new ImGuiController(gl, window, inputContext);
            _textureManager = new TextureManager(gl);
            _spriteRenderer = new SpriteRenderer(gl);
            ApplyGodotTheme();
        }

        private void OnProjectLoad(string projectPath)
        {
            _project = new Project(projectPath);
            var gameState = new GameState();
            var objectTypeManager = new ObjectTypeManager();
            var mapLoader = new MapLoader(objectTypeManager);
            _assetManager = new AssetManager();
            var scriptManager = new ScriptManager();
            var selectionManager = new SelectionManager();
            var toolManager = new ToolManager();
            var gameApi = new GameApi(gameState, objectTypeManager, mapLoader);

            var wall = new ObjectType("wall");
            wall.DefaultProperties["SpritePath"] = "assets/wall.png";
            objectTypeManager.RegisterObjectType(wall);

            var floor = new ObjectType("floor");
            floor.DefaultProperties["SpritePath"] = "assets/floor.png";
            objectTypeManager.RegisterObjectType(floor);

            toolManager.SetActiveTool(toolManager.Tools.FirstOrDefault(), _editorContext);

            _viewportPanel = new ViewportPanel(gl, gameApi, toolManager, selectionManager, _editorContext, _spriteRenderer, _textureManager);
            _assetBrowserPanel = new AssetBrowserPanel(_assetManager);
            _inspectorPanel = new InspectorPanel(gameApi, selectionManager, _editorContext);
            _objectBrowserPanel = new ObjectBrowserPanel(objectTypeManager, _editorContext);
            _scriptEditorPanel = new ScriptEditorPanel(scriptManager);
            _settingsPanel = new SettingsPanel();
            _toolboxPanel = new ToolboxPanel(toolManager, _editorContext);
            _menuBarPanel = new MenuBarPanel(gameApi, _editorContext);
            _mapControlsPanel = new MapControlsPanel(_editorContext);

            _appState = AppState.Editing;
        }

        private void OnRender(double deltaTime)
        {
            imGuiController?.Update((float)deltaTime);

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
                    _menuBarPanel.Draw();

                    _viewportPanel.Draw();
                    _assetBrowserPanel.Draw();
                    _inspectorPanel.Draw();
                    _objectBrowserPanel.Draw();
                    _scriptEditorPanel.Draw();
                    _toolboxPanel.Draw();
                    _mapControlsPanel.Draw();
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

        private void ApplyGodotTheme()
        {
            var style = ImGui.GetStyle();
            var colors = style.Colors;
            style.WindowRounding = 4.0f; style.FrameRounding = 4.0f; style.GrabRounding = 4.0f;
            style.PopupRounding = 4.0f; style.ScrollbarRounding = 4.0f; style.TabRounding = 4.0f;
            colors[(int)ImGuiCol.Text] = new System.Numerics.Vector4(0.90f, 0.90f, 0.90f, 1.00f);
            colors[(int)ImGuiCol.TextDisabled] = new System.Numerics.Vector4(0.60f, 0.60f, 0.60f, 1.00f);
            colors[(int)ImGuiCol.WindowBg] = new System.Numerics.Vector4(0.12f, 0.12f, 0.12f, 1.00f);
            colors[(int)ImGuiCol.ChildBg] = new System.Numerics.Vector4(0.12f, 0.12f, 0.12f, 1.00f);
            colors[(int)ImGuiCol.PopupBg] = new System.Numerics.Vector4(0.11f, 0.11f, 0.11f, 0.92f);
            colors[(int)ImGuiCol.Border] = new System.Numerics.Vector4(0.25f, 0.25f, 0.25f, 1.00f);
            colors[(int)ImGuiCol.BorderShadow] = new System.Numerics.Vector4(0.00f, 0.00f, 0.00f, 0.00f);
            colors[(int)ImGuiCol.FrameBg] = new System.Numerics.Vector4(0.20f, 0.20f, 0.20f, 1.00f);
            colors[(int)ImGuiCol.FrameBgHovered] = new System.Numerics.Vector4(0.25f, 0.25f, 0.25f, 1.00f);
            colors[(int)ImGuiCol.FrameBgActive] = new System.Numerics.Vector4(0.30f, 0.30f, 0.30f, 1.00f);
            colors[(int)ImGuiCol.TitleBg] = new System.Numerics.Vector4(0.10f, 0.10f, 0.10f, 1.00f);
            colors[(int)ImGuiCol.TitleBgActive] = new System.Numerics.Vector4(0.15f, 0.15f, 0.15f, 1.00f);
            colors[(int)ImGuiCol.TitleBgCollapsed] = new System.Numerics.Vector4(0.10f, 0.10f, 0.10f, 1.00f);
            colors[(int)ImGuiCol.MenuBarBg] = new System.Numerics.Vector4(0.10f, 0.10f, 0.10f, 1.00f);
            colors[(int)ImGuiCol.ScrollbarBg] = new System.Numerics.Vector4(0.20f, 0.20f, 0.20f, 1.00f);
            colors[(int)ImGuiCol.ScrollbarGrab] = new System.Numerics.Vector4(0.30f, 0.30f, 0.30f, 1.00f);
            colors[(int)ImGuiCol.ScrollbarGrabHovered] = new System.Numerics.Vector4(0.35f, 0.35f, 0.35f, 1.00f);
            colors[(int)ImGuiCol.ScrollbarGrabActive] = new System.Numerics.Vector4(0.40f, 0.40f, 0.40f, 1.00f);
            colors[(int)ImGuiCol.CheckMark] = new System.Numerics.Vector4(0.70f, 0.70f, 0.70f, 1.00f);
            colors[(int)ImGuiCol.SliderGrab] = new System.Numerics.Vector4(0.40f, 0.40f, 0.40f, 1.00f);
            colors[(int)ImGuiCol.SliderGrabActive] = new System.Numerics.Vector4(0.50f, 0.50f, 0.50f, 1.00f);
            colors[(int)ImGuiCol.Button] = new System.Numerics.Vector4(0.25f, 0.25f, 0.25f, 1.00f);
            colors[(int)ImGuiCol.ButtonHovered] = new System.Numerics.Vector4(0.30f, 0.30f, 0.30f, 1.00f);
            colors[(int)ImGuiCol.ButtonActive] = new System.Numerics.Vector4(0.35f, 0.35f, 0.35f, 1.00f);
            colors[(int)ImGuiCol.Header] = new System.Numerics.Vector4(0.20f, 0.20f, 0.20f, 1.00f);
            colors[(int)ImGuiCol.HeaderHovered] = new System.Numerics.Vector4(0.25f, 0.25f, 0.25f, 1.00f);
            colors[(int)ImGuiCol.HeaderActive] = new System.Numerics.Vector4(0.30f, 0.30f, 0.30f, 1.00f);
            colors[(int)ImGuiCol.Separator] = new System.Numerics.Vector4(0.25f, 0.25f, 0.25f, 1.00f);
            colors[(int)ImGuiCol.SeparatorHovered] = new System.Numerics.Vector4(0.30f, 0.30f, 0.30f, 1.00f);
            colors[(int)ImGuiCol.SeparatorActive] = new System.Numerics.Vector4(0.35f, 0.35f, 0.35f, 1.00f);
            colors[(int)ImGuiCol.ResizeGrip] = new System.Numerics.Vector4(0.25f, 0.25f, 0.25f, 1.00f);
            colors[(int)ImGuiCol.ResizeGripHovered] = new System.Numerics.Vector4(0.30f, 0.30f, 0.30f, 1.00f);
            colors[(int)ImGuiCol.ResizeGripActive] = new System.Numerics.Vector4(0.35f, 0.35f, 0.35f, 1.00f);
            colors[(int)ImGuiCol.Tab] = new System.Numerics.Vector4(0.20f, 0.20f, 0.20f, 1.00f);
            colors[(int)ImGuiCol.TabHovered] = new System.Numerics.Vector4(0.25f, 0.25f, 0.25f, 1.00f);
            colors[(int)ImGuiCol.DockingPreview] = new System.Numerics.Vector4(0.40f, 0.40f, 0.40f, 0.70f);
            colors[(int)ImGuiCol.DockingEmptyBg] = new System.Numerics.Vector4(0.20f, 0.20f, 0.20f, 1.00f);
            colors[(int)ImGuiCol.PlotLines] = new System.Numerics.Vector4(0.90f, 0.90f, 0.90f, 1.00f);
            colors[(int)ImGuiCol.PlotLinesHovered] = new System.Numerics.Vector4(1.00f, 1.00f, 1.00f, 1.00f);
            colors[(int)ImGuiCol.PlotHistogram] = new System.Numerics.Vector4(0.90f, 0.70f, 0.00f, 1.00f);
            colors[(int)ImGuiCol.PlotHistogramHovered] = new System.Numerics.Vector4(1.00f, 0.60f, 0.00f, 1.00f);
            colors[(int)ImGuiCol.TableHeaderBg] = new System.Numerics.Vector4(0.19f, 0.19f, 0.20f, 1.00f);
            colors[(int)ImGuiCol.TableBorderStrong] = new System.Numerics.Vector4(0.31f, 0.31f, 0.35f, 1.00f);
            colors[(int)ImGuiCol.TableBorderLight] = new System.Numerics.Vector4(0.23f, 0.23f, 0.25f, 1.00f);
            colors[(int)ImGuiCol.TableRowBg] = new System.Numerics.Vector4(0.00f, 0.00f, 0.00f, 0.00f);
            colors[(int)ImGuiCol.TableRowBgAlt] = new System.Numerics.Vector4(1.00f, 1.00f, 1.00f, 0.06f);
            colors[(int)ImGuiCol.TextSelectedBg] = new System.Numerics.Vector4(0.26f, 0.59f, 0.98f, 0.35f);
            colors[(int)ImGuiCol.DragDropTarget] = new System.Numerics.Vector4(1.00f, 1.00f, 0.00f, 0.90f);
            colors[(int)ImGuiCol.NavWindowingHighlight] = new System.Numerics.Vector4(1.00f, 1.00f, 1.00f, 0.70f);
            colors[(int)ImGuiCol.NavWindowingDimBg] = new System.Numerics.Vector4(0.80f, 0.80f, 0.80f, 0.20f);
            colors[(int)ImGuiCol.ModalWindowDimBg] = new System.Numerics.Vector4(0.80f, 0.80f, 0.80f, 0.35f);
        }
    }

    public enum AppState
    {
        MainMenu,
        Editing,
        Settings
    }
}
