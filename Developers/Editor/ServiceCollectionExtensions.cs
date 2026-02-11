using Editor.UI;
using Microsoft.Extensions.DependencyInjection;
using Shared;

namespace Editor
{
    public static class ServiceCollectionExtensions
    {
        public static IServiceCollection AddEditorServices(this IServiceCollection services)
        {
            services.AddSingleton<Editor>(provider =>
                new Editor(
                    provider,
                    provider.GetRequiredService<MainPanel>(),
                    provider.GetRequiredService<MenuBarPanel>(),
                    provider.GetRequiredService<ViewportPanel>(),
                    provider.GetRequiredService<TextureManager>(),
                    provider.GetRequiredService<IProjectService>(),
                    provider.GetRequiredService<SettingsPanel>(),
                    provider.GetRequiredService<IRunService>(),
                    provider.GetRequiredService<IEditorSettingsManager>()
                )
            );
            services.AddSingleton<ProjectHolder>();
            services.AddSingleton<IProject>(sp => sp.GetRequiredService<ProjectHolder>());
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
            services.AddSingleton<SpriteRenderer>();
            services.AddSingleton<IProjectService, ProjectService>(provider =>
                new ProjectService(
                    provider.GetRequiredService<ProjectHolder>(),
                    provider.GetRequiredService<IObjectTypeManager>(),
                    provider.GetRequiredService<ToolManager>(),
                    provider.GetRequiredService<EditorContext>(),
                    provider.GetRequiredService<IUIService>(),
                    provider.GetRequiredService<IDreamMakerLoader>(),
                    provider.GetRequiredService<IJsonService>()
                )
            );
            services.AddSingleton<IUIService, UIService>();
            services.AddSingleton<IRunService, RunService>();
            services.AddSingleton<IEditorSettingsManager, EditorSettingsManager>();
            services.AddSingleton<Launcher.IProcessService, Launcher.ProcessService>();

            return services;
        }

        public static IServiceCollection AddUiPanels(this IServiceCollection services)
        {
            services.AddSingleton<MainPanel>(provider =>
                new MainPanel(
                    provider.GetRequiredService<ProjectsPanel>(),
                    provider.GetRequiredService<ServerBrowserPanel>(),
                    provider.GetRequiredService<ViewportPanel>(),
                    provider.GetRequiredService<ToolbarPanel>(),
                    provider.GetRequiredService<InspectorPanel>(),
                    provider.GetRequiredService<ObjectBrowserPanel>(),
                    provider.GetRequiredService<AssetBrowserPanel>(),
                    provider.GetRequiredService<SceneHierarchyPanel>(),
                    provider.GetRequiredService<EditorContext>(),
                    provider.GetRequiredService<EditorLaunchOptions>(),
                    provider.GetRequiredService<IUIService>()
                )
            );
            services.AddSingleton<ProjectsPanel>(provider =>
                new ProjectsPanel(
                    provider.GetRequiredService<EditorContext>(),
                    provider.GetRequiredService<IProjectManager>(),
                    provider.GetRequiredService<LocalizationManager>(),
                    provider.GetRequiredService<IProjectService>()
                )
            );
            services.AddSingleton<MenuBarPanel>(provider =>
                new MenuBarPanel(
                    provider.GetRequiredService<IGameApi>(),
                    provider.GetRequiredService<EditorContext>(),
                    provider.GetRequiredService<BuildService>(),
                    provider.GetRequiredService<IDmmService>(),
                    provider.GetRequiredService<IMapLoader>(),
                    provider.GetRequiredService<LocalizationManager>(),
                    provider.GetRequiredService<IProjectManager>(),
                    provider.GetRequiredService<IProjectService>(),
                    provider.GetRequiredService<SettingsPanel>()
                )
            );
            services.AddSingleton<ViewportPanel>(provider =>
                new ViewportPanel(
                    provider.GetRequiredService<ToolManager>(),
                    provider.GetRequiredService<SelectionManager>(),
                    provider.GetRequiredService<EditorContext>(),
                    provider.GetRequiredService<IGameApi>(),
                    provider.GetRequiredService<SpriteRenderer>(),
                    provider.GetRequiredService<TextureManager>(),
                    provider.GetRequiredService<IObjectTypeManager>(),
                    provider.GetRequiredService<IEditorSettingsManager>()
                )
            );
            services.AddSingleton<AssetBrowserPanel>();
            services.AddSingleton<InspectorPanel>();
            services.AddSingleton<ObjectBrowserPanel>();
            services.AddSingleton<ScriptEditorPanel>();
            services.AddSingleton<SettingsPanel>();
            services.AddSingleton<ToolbarPanel>();
            services.AddSingleton<MapControlsPanel>();
            services.AddSingleton<BuildPanel>();
            services.AddSingleton<SceneHierarchyPanel>();
            services.AddSingleton<ServerBrowserPanel>(provider =>
                new ServerBrowserPanel(
                    provider.GetRequiredService<IServerDiscoveryService>()
                )
            );

            return services;
        }
    }
}
