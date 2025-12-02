using Silk.NET.Windowing;
using Silk.NET.OpenGL;
using Silk.NET.Maths;
using Core;
using ImGuiNET;
using Silk.NET.OpenGL.Extensions.ImGui;
using Silk.NET.Input;
using Editor.UI;
using System.Linq;
using Microsoft.Extensions.DependencyInjection;
using System;

namespace Editor
{
    public class Editor
    {
        private IWindow? window;
        private GL? gl;
        private IInputContext? inputContext;
        private ImGuiController? imGuiController;

        private Project? _project;
        private IServiceProvider? _serviceProvider;

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
            if (_serviceProvider != null)
            {
                var assetManager = _serviceProvider.GetRequiredService<AssetManager>();
                foreach (var path in paths)
                {
                    assetManager.ImportAsset(path);
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
                _editorContext.Gl = gl;
            }
        }

        private void ConfigureServices(IServiceCollection services)
        {
            services.AddSingleton(_project);
            services.AddSingleton<GameState>();
            services.AddSingleton<ObjectTypeManager>();
            services.AddSingleton<MapLoader>();
            services.AddSingleton<AssetManager>();
            services.AddSingleton<SelectionManager>();
            services.AddSingleton<ToolManager>();
            services.AddSingleton<IMapApi, MapApi>();
            services.AddSingleton<IObjectApi, ObjectApi>();
            services.AddSingleton<IScriptApi, ScriptApi>();
            services.AddSingleton<IStandardLibraryApi, StandardLibraryApi>();
            services.AddSingleton<IGameApi, GameApi>();
            services.AddSingleton<OpenDreamCompilerService>();
            services.AddSingleton<DmmService>();
            services.AddSingleton<BuildService>();
            services.AddSingleton(_editorContext);

            // UI Panels
            services.AddSingleton<MenuBarPanel>();
            services.AddSingleton<ViewportPanel>();
            services.AddSingleton<AssetBrowserPanel>();
            services.AddSingleton<InspectorPanel>();
            services.AddSingleton<ObjectBrowserPanel>();
            services.AddSingleton<ScriptEditorPanel>();
            services.AddSingleton<SettingsPanel>();
            services.AddSingleton<ToolboxPanel>();
            services.AddSingleton<MapControlsPanel>();
            services.AddSingleton<BuildPanel>();
        }

        private void OnProjectLoad(string projectPath)
        {
            _project = new Project(projectPath);
            var services = new ServiceCollection();
            ConfigureServices(services);
            _serviceProvider = services.BuildServiceProvider();

            var objectTypeManager = _serviceProvider.GetRequiredService<ObjectTypeManager>();
            var wall = new ObjectType("wall");
            wall.DefaultProperties["SpritePath"] = "assets/wall.png";
            objectTypeManager.RegisterObjectType(wall);

            var floor = new ObjectType("floor");
            floor.DefaultProperties["SpritePath"] = "assets/floor.png";
            objectTypeManager.RegisterObjectType(floor);

            var toolManager = _serviceProvider.GetRequiredService<ToolManager>();
            toolManager.SetActiveTool(toolManager.Tools.FirstOrDefault(), _editorContext);

            _menuBarPanel = _serviceProvider.GetRequiredService<MenuBarPanel>();
            _viewportPanel = _serviceProvider.GetRequiredService<ViewportPanel>();
            _assetBrowserPanel = _serviceProvider.GetRequiredService<AssetBrowserPanel>();
            _inspectorPanel = _serviceProvider.GetRequiredService<InspectorPanel>();
            _objectBrowserPanel = _serviceProvider.GetRequiredService<ObjectBrowserPanel>();
            _scriptEditorPanel = _serviceProvider.GetRequiredService<ScriptEditorPanel>();
            _settingsPanel = _serviceProvider.GetRequiredService<SettingsPanel>();
            _toolboxPanel = _serviceProvider.GetRequiredService<ToolboxPanel>();
            _mapControlsPanel = _serviceProvider.GetRequiredService<MapControlsPanel>();
            _buildPanel = _serviceProvider.GetRequiredService<BuildPanel>();

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
                    _menuBarPanel.Draw();
                    _assetBrowserPanel.Draw();
                    _inspectorPanel.Draw();
                    _objectBrowserPanel.Draw();
                    _toolboxPanel.Draw();
                    _mapControlsPanel.Draw();
                    _buildPanel.Draw();

                    ImGui.Begin("MainView");
                    if (ImGui.BeginTabBar("FileTabs"))
                    {
                        for (int i = 0; i < _editorContext.OpenFiles.Count; i++)
                        {
                            var file = _editorContext.OpenFiles[i];
                            bool isOpen = true;
                            if (ImGui.BeginTabItem(System.IO.Path.GetFileName(file.Path), ref isOpen))
                            {
                                switch (file.Type)
                                {
                                    case FileType.Map:
                                        _viewportPanel.Draw(file.Path);
                                        break;
                                    case FileType.Script:
                                        _scriptEditorPanel.Draw(file.Path);
                                        break;
                                }
                                ImGui.EndTabItem();
                            }

                            if (!isOpen)
                            {
                                _editorContext.OpenFiles.RemoveAt(i);
                            }
                        }
                        ImGui.EndTabBar();
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
