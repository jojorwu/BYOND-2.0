using Microsoft.Extensions.DependencyInjection;
using Shared.Interfaces;
using Shared.Services;
using System;

namespace Launcher
{
    public class LauncherModule : IEngineModule
    {
        public void RegisterServices(IServiceCollection services)
        {
            services.AddSingleton<Launcher>();
            // Add any other developer-launcher specific services here
        }

        public void PreTick() { }
        public void PostTick() { }
    }
}
