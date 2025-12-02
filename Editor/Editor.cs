using Silk.NET.Windowing;
using Silk.NET.OpenGL;
using Silk.NET.Maths;
using Core;
using ImGuiNET;
using Silk.NET.OpenGL.Extensions.ImGui;
using Silk.NET.Input;
using Editor.UI;
using Microsoft.Extensions.DependencyInjection;

namespace Editor
{
    public class Editor
    {
        private IWindow? window;
        private GL? gl;
        private IInputContext? inputContext;
        private ImGuiController? imGuiController;

        private IServiceScope? _projectScope;
        private IServiceProvider _serviceProvider;

        // Panels (resolved from DI)
        private MainMenuPanel _mainMenuPanel = null!;
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

        public Editor()
        {
            var services = new ServiceCollection();
            ConfigureServices(services);
            _serviceProvider = services.BuildServiceProvider();

            _mainMenuPanel = _serviceProvider.GetRequiredService<MainMenuPanel>();
        }

        private void ConfigureServices(IServiceCollection services)
        {
            services.AddSingleton<EditorContext>();
            services.AddSingleton<MainMenuPanel>();
            services.AddScoped<MenuBarPanel>();
            services.AddScoped<ViewportPanel>();
            services.AddScoped<AssetBrowserPanel>();
            services.AddScoped<InspectorPanel>();
            services.AddScoped<ObjectBrowserPanel>();
            services.AddScoped<ScriptEditorPanel>();
            services.AddScoped<SettingsPanel>();
            services.AddScoped<ToolboxPanel>();
            services.AddScoped<MapControlsPanel>();
            services.AddScoped<BuildPanel>();
            services.AddScoped<BuildService>();
            services.AddScoped<DmmService>();
            services.AddScoped<AssetManager>();
            services.AddScoped<SelectionManager>();
            services.AddScoped<ToolManager>();
            services.AddCoreServices();
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

            window.Run();
        }

        private void OnLoad()
        {
            if (window != null)
            {
                gl = window.CreateOpenGL();
                inputContext = window.CreateInput();
                imGuiController = new ImGuiController(gl, window, inputContext);
                _serviceProvider.GetRequiredService<EditorContext>().GL = gl;
            }
        }

        private void OnProjectLoad(string projectPath)
        {
            _projectScope = _serviceProvider.CreateScope();
            var scopeProvider = _projectScope.ServiceProvider;

            var project = new Project(projectPath);
            scopeProvider.GetRequiredService<EditorContext>().Project = project;

            _menuBarPanel = scopeProvider.GetRequiredService<MenuBarPanel>();
            _viewportPanel = scopeProvider.GetRequiredService<ViewportPanel>();
            _assetBrowserPanel = scopeProvider.GetRequiredService<AssetBrowserPanel>();
            _inspectorPanel = scopeProvider.GetRequiredService<InspectorPanel>();
            _objectBrowserPanel = scopeProvider.GetRequiredService<ObjectBrowserPanel>();
            _scriptEditorPanel = scopeProvider.GetRequiredService<ScriptEditorPanel>();
            _settingsPanel = scopeProvider.GetRequiredService<SettingsPanel>();
            _toolboxPanel = scopeProvider.GetRequiredService<ToolboxPanel>();
            _mapControlsPanel = scopeProvider.GetRequiredService<MapControlsPanel>();
            _buildPanel = scopeProvider.GetRequiredService<BuildPanel>();

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
                        var editorContext = _serviceProvider.GetRequiredService<EditorContext>();
                        for (int i = 0; i < editorContext.OpenFiles.Count; i++)
                        {
                            var file = editorContext.OpenFiles[i];
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
                                editorContext.OpenFiles.RemoveAt(i);
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
            _projectScope?.Dispose();
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
