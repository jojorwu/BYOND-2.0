using Editor.UI;
using Microsoft.Extensions.DependencyInjection;
using Shared;
using System.Linq;

namespace Editor
{
    public static class ServiceCollectionExtensions
    {
        public static IServiceCollection AddEditorServices(this IServiceCollection services)
        {
            services.AddSingleton<Editor>(provider =>
                new Editor(
                    provider,
                    provider.GetServices<IUiPanel>(),
                    provider.GetRequiredService<TextureManager>()
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
            services.AddSingleton<IProjectService, ProjectService>();
            services.AddSingleton<IUIService, UIService>();

            return services;
        }

        public static IServiceCollection AddUiPanels(this IServiceCollection services)
        {
            var panelTypes = typeof(ServiceCollectionExtensions).Assembly.GetTypes()
                .Where(t => t.IsClass && !t.IsAbstract && t.GetInterfaces().Contains(typeof(IUiPanel)));

            foreach (var type in panelTypes)
            {
                services.AddSingleton(type);
                services.AddSingleton(typeof(IUiPanel), sp => sp.GetRequiredService(type));
            }

            return services;
        }
    }
}
