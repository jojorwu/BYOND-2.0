using Silk.NET.Windowing;
using Silk.NET.OpenGL;
using Silk.NET.Maths;
using Core;
using ImGuiNET;
using Silk.NET.OpenGL.Extensions.ImGui;
using Silk.NET.Input;
using System;
using System.Numerics;
using System.Collections.Generic;
using System.Linq;
using System.IO;
using Editor.UI;

namespace Editor
{
    public class Editor
    {
        private enum AppState
        {
            MainMenu,
            Editing,
            Settings
        }

        private IWindow? window;
        private GL? gl;
        private IInputContext? inputContext;
        private ImGuiController? imGuiController;
        private TextureManager? _textureManager;
        private SpriteRenderer? _spriteRenderer;

        private readonly ToolManager _toolManager = new ToolManager();
        private readonly EditorState _editorState = new EditorState();
        private readonly SelectionManager _selectionManager = new SelectionManager();
        private InspectorPanel? _inspectorPanel;
        private ObjectBrowserPanel? _objectBrowserPanel;
        private MainMenuPanel _mainMenuPanel = null!;
        private SettingsPanel _settingsPanel = null!;
        private ScriptEditorPanel? _scriptEditorPanel;
        private MenuBarPanel? _menuBarPanel;
        private ToolboxPanel? _toolboxPanel;
        private AssetBrowserPanel? _assetBrowserPanel;
        private ViewportPanel? _viewportPanel;
        private AssetManager? _assetManager;
        private AssetBrowser? _assetBrowser;
        private ScriptManager? _scriptManager;
        private ObjectTypeManager? _objectTypeManager;
        private MapLoader? _mapLoader;
        private GameApi? _gameApi;
        private EngineSettings _engineSettings;
        private GameState _gameState = new GameState();
        private EditorContext? _editorContext;

        public int CurrentZLevel { get; private set; } = 0;

        private AppState _appState = AppState.MainMenu;
        private string _newTypeName = string.Empty;

        public Editor()
        {
            _engineSettings = EngineSettings.Load();
            _mainMenuPanel = new MainMenuPanel();
            _settingsPanel = new SettingsPanel(_engineSettings);
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
            if (_assetManager == null) return;
            foreach (var path in paths) _assetManager.ImportAsset(path);
        }

        private void OnLoad()
        {
            if (window != null)
            {
                gl = window.CreateOpenGL();
                inputContext = window.CreateInput();
                imGuiController = new ImGuiController(gl, window, inputContext);
                _textureManager = new TextureManager(gl);
                _spriteRenderer = new SpriteRenderer(gl);
                _toolManager.SetActiveTool(_toolManager.Tools.FirstOrDefault(), this);

                if (!string.IsNullOrEmpty(_engineSettings.LastProjectPath) && Directory.Exists(_engineSettings.LastProjectPath))
                {
                    LoadProject(_engineSettings.LastProjectPath);
                    _appState = AppState.Editing;
                }
            }
            ApplyGodotTheme();
        }

        private void OnRender(double deltaTime)
        {
            imGuiController?.Update((float)deltaTime);
            Draw();
        }

        private void Draw()
        {
            gl?.ClearColor(0.12f, 0.12f, 0.12f, 1.00f);
            gl?.Clear(ClearBufferMask.ColorBufferBit);

            switch (_appState)
            {
                case AppState.MainMenu:
                    var mainMenuAction = _mainMenuPanel.Draw();
                    HandleMainMenuAction(mainMenuAction);
                    break;
                case AppState.Editing:
                    DrawEditor();
                    break;
                case AppState.Settings:
                    var settingsAction = _settingsPanel.Draw();
                    if (settingsAction == SettingsAction.Back)
                    {
                        _appState = AppState.MainMenu;
                    }
                    break;
            }

            imGuiController?.Render();
        }

        private void HandleMainMenuAction(MainMenuAction action)
        {
            switch (action)
            {
                case MainMenuAction.NewProject:
                    var project = Project.Create("Projects", _mainMenuPanel.SelectedProject);
                    LoadProject(project.RootPath);
                    _appState = AppState.Editing;
                    break;
                case MainMenuAction.LoadProject:
                    LoadProject(_mainMenuPanel.SelectedProject);
                    _appState = AppState.Editing;
                    break;
                case MainMenuAction.Settings:
                    _appState = AppState.Settings;
                    break;
                case MainMenuAction.Exit:
                    window?.Close();
                    break;
            }
        }

        private void DrawEditor()
        {
            ImGui.DockSpaceOverViewport(ImGui.GetMainViewport(), ImGuiDockNodeFlags.PassthruCentralNode);
            var menuBarAction = _menuBarPanel?.Draw() ?? MenuBarAction.None;
            HandleMenuBarAction(menuBarAction);
            _objectBrowserPanel?.Draw();
            _inspectorPanel?.Draw();
            _assetBrowserPanel?.Draw();
            _toolboxPanel?.Draw();

            _viewportPanel?.Draw();

            _scriptEditorPanel?.Draw();
        }

        private void LoadProject(string projectPath)
        {
            var project = new Project(projectPath);
            _editorContext = new EditorContext();

            _assetManager = new AssetManager(project);
            _assetBrowser = new AssetBrowser(_assetManager);
            _scriptManager = new ScriptManager(project);
            _objectTypeManager = new ObjectTypeManager(project);
            _objectTypeManager.LoadTypes();
            _mapLoader = new MapLoader(_objectTypeManager, project);
            _gameApi = new GameApi(_gameState, _objectTypeManager, _mapLoader, project);

            _inspectorPanel = new InspectorPanel(_selectionManager, _objectTypeManager, _gameApi, _editorContext);
            _objectBrowserPanel = new ObjectBrowserPanel(_objectTypeManager, _editorContext);
            _scriptEditorPanel = new ScriptEditorPanel(_scriptManager);
            _menuBarPanel = new MenuBarPanel();
            _toolboxPanel = new ToolboxPanel(_toolManager, this);
            _assetBrowserPanel = new AssetBrowserPanel(_assetBrowser);
            _viewportPanel = new ViewportPanel(_gameState, _spriteRenderer!, _textureManager!, _toolManager, this, _gameApi, _selectionManager);

            _engineSettings.LastProjectPath = projectPath;
            _engineSettings.Save();
        }

        private void HandleMenuBarAction(MenuBarAction action)
        {
            switch (action)
            {
                case MenuBarAction.SaveMap:
                    _gameApi.SaveMap("maps/default.json");
                    break;
                case MenuBarAction.LoadMap:
                    _gameApi.LoadMap("maps/default.json");
                    break;
                case MenuBarAction.GoToMainMenu:
                    _appState = AppState.MainMenu;
                    break;
                case MenuBarAction.OpenSettings:
                    _appState = AppState.Settings;
                    break;
            }
        }

        private void OnClose()
        {
            _spriteRenderer?.Dispose();
            _textureManager?.Dispose();
            imGuiController?.Dispose();
            gl?.Dispose();
        }

        private void ApplyGodotTheme()
        {
            var style = ImGui.GetStyle();
            var colors = style.Colors;
            style.WindowRounding = 4.0f; style.FrameRounding = 4.0f; style.GrabRounding = 4.0f;
            style.PopupRounding = 4.0f; style.ScrollbarRounding = 4.0f; style.TabRounding = 4.0f;
            colors[(int)ImGuiCol.Text] = new Vector4(0.90f, 0.90f, 0.90f, 1.00f);
            colors[(int)ImGuiCol.WindowBg] = new Vector4(0.12f, 0.12f, 0.12f, 1.00f);
            colors[(int)ImGuiCol.ChildBg] = new Vector4(0.14f, 0.14f, 0.14f, 1.00f);
            colors[(int)ImGuiCol.PopupBg] = new Vector4(0.10f, 0.10f, 0.10f, 0.95f);
            colors[(int)ImGuiCol.Border] = new Vector4(0.05f, 0.05f, 0.05f, 0.50f);
            colors[(int)ImGuiCol.FrameBg] = new Vector4(0.20f, 0.20f, 0.22f, 1.00f);
            colors[(int)ImGuiCol.FrameBgHovered] = new Vector4(0.25f, 0.25f, 0.27f, 1.00f);
            colors[(int)ImGuiCol.FrameBgActive] = new Vector4(0.30f, 0.30f, 0.32f, 1.00f);
            colors[(int)ImGuiCol.TitleBg] = new Vector4(0.10f, 0.10f, 0.10f, 1.00f);
            colors[(int)ImGuiCol.TitleBgActive] = new Vector4(0.15f, 0.15f, 0.15f, 1.00f);
            colors[(int)ImGuiCol.MenuBarBg] = new Vector4(0.10f, 0.10f, 0.10f, 1.00f);
            var activeColor = new Vector4(0.26f, 0.59f, 0.98f, 1.00f);
            colors[(int)ImGuiCol.Header] = new Vector4(0.20f, 0.20f, 0.22f, 1.00f);
            colors[(int)ImGuiCol.HeaderHovered] = new Vector4(0.26f, 0.59f, 0.98f, 0.50f);
            colors[(int)ImGuiCol.HeaderActive] = activeColor;
            colors[(int)ImGuiCol.Button] = new Vector4(0.20f, 0.20f, 0.22f, 1.00f);
            colors[(int)ImGuiCol.ButtonHovered] = new Vector4(0.26f, 0.59f, 0.98f, 0.80f);
            colors[(int)ImGuiCol.ButtonActive] = activeColor;
            colors[(int)ImGuiCol.Tab] = new Vector4(0.12f, 0.12f, 0.12f, 1.00f);
            colors[(int)ImGuiCol.TabHovered] = new Vector4(0.26f, 0.59f, 0.98f, 0.80f);
            colors[(int)ImGuiCol.TabActive] = new Vector4(0.20f, 0.20f, 0.22f, 1.00f);
            colors[(int)ImGuiCol.TabUnfocused] = new Vector4(0.12f, 0.12f, 0.12f, 1.00f);
            colors[(int)ImGuiCol.TabUnfocusedActive] = new Vector4(0.20f, 0.20f, 0.22f, 1.00f);
            colors[(int)ImGuiCol.CheckMark] = activeColor;
            colors[(int)ImGuiCol.SliderGrab] = activeColor;
            colors[(int)ImGuiCol.SliderGrabActive] = new Vector4(0.20f, 0.50f, 0.90f, 1.00f);
            colors[(int)ImGuiCol.DockingPreview] = new Vector4(0.26f, 0.59f, 0.98f, 0.70f);
        }
    }
}
