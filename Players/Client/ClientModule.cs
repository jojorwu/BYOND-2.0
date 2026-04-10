using Microsoft.Extensions.DependencyInjection;
using Shared.Interfaces;
using Shared.Services;
using Client.Services;
using System;

namespace Client
{
    public class ClientModule : IEngineModule
    {
        public void RegisterServices(IServiceCollection services)
        {
            services.AddSingleton<Game>();
            services.AddSingleton<IClientObjectManager, ClientObjectManager>();

            // Register other client-specific services here as they are identified
        }

        public void PreTick() { }
        public void PostTick() { }
    }
}
