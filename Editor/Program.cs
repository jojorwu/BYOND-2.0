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
            var launchOptions = ParseArguments(args);

            var services = new ServiceCollection();
            ConfigureServices(services, launchOptions);
            var serviceProvider = services.BuildServiceProvider();

            var editor = serviceProvider.GetRequiredService<Editor>();
            editor.Run();
        }

        private static EditorLaunchOptions ParseArguments(string[] args)
        {
            var options = new EditorLaunchOptions();
            for (int i = 0; i < args.Length; i++)
            {
                if (args[i] == "--panel" && i + 1 < args.Length)
                {
                    options.InitialPanel = args[i + 1];
                    i++; // Skip the next argument
                }
            }
            return options;
        }

        private static void ConfigureServices(IServiceCollection services, EditorLaunchOptions launchOptions)
        {
            services.AddSingleton(launchOptions);
            services.AddSingleton<Shared.IJsonService, DMCompiler.Json.JsonService>();
            services.AddSingleton<Shared.ICompilerService, DMCompiler.CompilerService>();
            services.AddCoreServices();
            services.AddEditorServices();
            services.AddUiPanels();
        }
    }
}
