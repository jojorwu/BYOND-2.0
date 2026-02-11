using Microsoft.Extensions.DependencyInjection;
using Shared.Interfaces;
using Shared.Messaging;
using Shared.Services;

namespace Shared.Services
{
    public static class SharedServiceCollectionExtensions
    {
        public static IServiceCollection AddSharedEngineServices(this IServiceCollection services)
        {
            services.AddSingleton<IEngineManager, EngineManager>();
            services.AddSingleton<IEventBus, EventBus>();
            services.AddSingleton<IComputeService, ComputeService>();
            services.AddSingleton<IPluginManager, PluginManager>();
            services.AddSingleton<ICommandDispatcher, CommandDispatcher>();
            services.AddSingleton<IProfilingService, ProfilingService>();
            services.AddSingleton<ISnapshotProvider, SnapshotProvider>();
            return services;
        }
    }
}
