using Shared.Models;
using Shared.Enums;
using Shared.Operations;
using Microsoft.Extensions.DependencyInjection;
using Shared.Interfaces;
using Shared.Services;

namespace Shared.Services
{
    public static class SharedEngineServiceExtensions
    {
        public static IServiceCollection AddSharedEngineServices(this IServiceCollection services)
        {
            services.AddSingleton<IEngineManager, EngineManager>();
            services.AddSingleton<IComputeService, ComputeService>();
            return services;
        }
    }
}
