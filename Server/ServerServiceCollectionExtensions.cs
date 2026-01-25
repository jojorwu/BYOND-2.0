using DMCompiler;
using Microsoft.Extensions.DependencyInjection;
using Shared;

namespace Server
{
    public static class ServerServiceCollectionExtensions
    {
        public static IServiceCollection AddServerServices(this IServiceCollection services)
        {
            services.AddSingleton<ICompilerService, OpenDreamCompilerService>();
            services.AddSingleton<IDmmParserService, DMMParserService>();
            return services;
        }
    }
}
