using Editor.UI;
using Microsoft.Extensions.DependencyInjection;
using Shared;

namespace Editor
{
    public static class ServiceCollectionExtensions
    {
        public static IServiceCollection AddEditorServices(this IServiceCollection services)
        {
            services.AddSingleton<Editor>();
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

            return services;
        }

        public static IServiceCollection AddUiPanels(this IServiceCollection services)
        {
            services.AddSingleton<ProjectManagerPanel>();
            services.AddSingleton<MenuBarPanel>();
            services.AddSingleton<ViewportPanel>(provider =>
                new ViewportPanel(
                    provider.GetRequiredService<ToolManager>(),
                    provider.GetRequiredService<SelectionManager>(),
                    provider.GetRequiredService<EditorContext>(),
                    provider.GetRequiredService<IGameApi>(),
                    provider.GetRequiredService<SpriteRenderer>(),
                    provider.GetRequiredService<TextureManager>(),
                    provider.GetRequiredService<IObjectTypeManager>()
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
            services.AddSingleton<ServerBrowserPanel>();

            return services;
        }
    }
}
