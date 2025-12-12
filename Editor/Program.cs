using Shared;
using Microsoft.Extensions.DependencyInjection;
using Editor.UI;
using Silk.NET.OpenGL;
using System;
using Core;
using Core.VM.Runtime;

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
            // Core services
            services.AddSingleton<IGameState, GameState>();
            services.AddSingleton<IObjectTypeManager, ObjectTypeManager>();
            services.AddSingleton<Ss14MapLoader>();
            services.AddSingleton<IMapLoader, MapLoader>();
            services.AddSingleton<IMapApi, MapApi>();
            services.AddSingleton<IObjectApi, ObjectApi>();
            services.AddSingleton<IScriptApi, ScriptApi>();
            services.AddSingleton<IStandardLibraryApi, StandardLibraryApi>();
            services.AddSingleton<IGameApi, GameApi>();
            services.AddSingleton<IDmmService, DmmService>();
            services.AddSingleton<ICompilerService, OpenDreamCompilerService>();
            services.AddSingleton<DreamVM>();
            services.AddSingleton<IDreamVM>(provider => provider.GetRequiredService<DreamVM>());
            services.AddSingleton<IDreamMakerLoader, DreamMakerLoader>();
            services.AddSingleton<IScriptManager, ScriptManager>();
            services.AddSingleton<IScriptSystem, Core.Scripting.CSharp.CSharpSystem>();
            services.AddSingleton<IScriptSystem, Core.Scripting.LuaSystem.LuaSystem>();
        services.AddSingleton<IScriptSystem>(provider =>
            new Core.Scripting.DM.DmSystem(
                provider.GetRequiredService<IObjectTypeManager>(),
                provider.GetRequiredService<IDreamMakerLoader>(),
                provider.GetRequiredService<ICompilerService>(),
                provider.GetRequiredService<IDreamVM>(),
                () => provider.GetRequiredService<IScriptHost>()
            )
        );


            // Editor services
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

            // UI Panels
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
            services.AddSingleton<SpriteRenderer>();
        }
    }
}
