using Microsoft.Extensions.DependencyInjection;
using Shared.Interfaces;
using Shared.Attributes;
using Shared.Services;
using System.Collections.Generic;

namespace Editor
{
    public class EditorModule : IEngineModule
    {
        public void RegisterServices(IServiceCollection services)
        {
            services.AddSingleton<EditorState>();
            services.AddSingleton<EditorContext>();

            // UI Services
            services.AddSingleton<IEditorUIService, EditorUIService>();

            // Tools
            services.AddSingleton<IToolManager, ToolManager>();

            // Application
            services.AddSingleton<EditorApplication>();
            services.AddHostedService(sp => sp.GetRequiredService<EditorApplication>());
        }

        public void PreTick() { }
        public void PostTick() { }
    }
}
