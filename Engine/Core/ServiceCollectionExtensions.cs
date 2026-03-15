using Core.Api;
using Core.Maps;
using Core.Players;
using Core.Regions;
using Core.VM.Runtime;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Shared;
using Shared.Interfaces;
using Shared.Services;

namespace Core
{
    public static class ServiceCollectionExtensions
    {
        /// <summary>
        /// Registers all core engine services using the modular approach.
        /// </summary>
        public static IServiceCollection AddCoreServices(this IServiceCollection services)
        {
            return services
                .AddSharedEngineServices()
                .AddEngineModule<CoreModule>();
        }
    }
}
