using Core;
using Core.VM.Runtime;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Hosting;
using Shared;
using Shared.Interfaces;
using Shared.Services;
using System;

namespace Server
{
    public static class ServiceCollectionExtensions
    {
        /// <summary>
        /// Registers all server-specific hosted services, including networking, game loop, and the main application coordinator.
        /// </summary>
        public static IServiceCollection AddServerHostedServices(this IServiceCollection services)
        {
            services.AddEngineModule<ServerModule>();

            services.AddSingleton<ServerApplication>();
            services.AddHostedService(provider => provider.GetRequiredService<ServerApplication>());

            return services;
        }
    }
}
