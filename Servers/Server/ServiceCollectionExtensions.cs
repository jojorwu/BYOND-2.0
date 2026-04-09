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
        /// Registers all server-specific hosted services using the modular approach.
        /// </summary>
        public static IServiceCollection AddServerHostedServices(this IServiceCollection services)
        {
            return services.AddEngineModule<ServerModule>();
        }
    }
}
