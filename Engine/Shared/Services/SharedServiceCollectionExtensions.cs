using Microsoft.Extensions.DependencyInjection;

namespace Shared
{
    public static class SharedServiceCollectionExtensions
    {
        public static IServiceCollection AddSharedEngineServices(this IServiceCollection services)
        {
            services.AddSingleton<IEngineManager, EngineManager>();

            // Other shared services can be added here as the project grows
            // e.g., IJsonService, IPathSanitizer, etc.

            return services;
        }
    }
}
