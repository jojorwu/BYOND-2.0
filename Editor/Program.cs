using Microsoft.Extensions.DependencyInjection;
using Core;
using Editor.UI;
using Silk.NET.OpenGL;
using System;

namespace Editor
{
    class Program
    {
        static void Main(string[] args)
        {
            var services = new ServiceCollection();
            ConfigureServices(services);
            var serviceProvider = services.BuildServiceProvider();

            var editor = serviceProvider.GetRequiredService<Editor>();
            editor.Run();
        }

        private static void ConfigureServices(IServiceCollection services)
        {
            services.AddSingleton<Editor>();
            services.AddSingleton<IGame, Game>();
            services.AddSingleton(provider =>
            {
                var editor = provider.GetRequiredService<Editor>();
                return editor.gl;
            });
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
    }
}
