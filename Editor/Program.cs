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
            services.AddCoreServices();
            services.AddEditorServices();
            services.AddUiPanels();
        }
    }
}
