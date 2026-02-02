using Microsoft.Extensions.DependencyInjection;
using Shared.Services;
using Shared.Interfaces;
using Client.Graphics;
using Client.Assets;
using Client.UI;

namespace Client
{
    public static class ServiceCollectionExtensions
    {
        public static IServiceCollection AddClientServices(this IServiceCollection services)
        {
            services.AddSharedEngineServices();

            services.AddSingleton<Game>();
            services.AddSingleton<IClient>(p => p.GetRequiredService<Game>());

            // Client-side services
            services.AddSingleton<TextureCache>();
            services.AddSingleton<IEngineService>(p => p.GetRequiredService<TextureCache>());

            services.AddSingleton<CSharpShaderManager>();
            services.AddSingleton<IEngineService>(p => p.GetRequiredService<CSharpShaderManager>());

            services.AddSingleton<DmiCache>();
            services.AddSingleton<IEngineService>(p => p.GetRequiredService<DmiCache>());

            services.AddSingleton<IconCache>();
            services.AddSingleton<IEngineService>(p => p.GetRequiredService<IconCache>());

            services.AddSingleton<ClientApplication>();
            services.AddHostedService(p => p.GetRequiredService<ClientApplication>());

            return services;
        }
    }

    public interface IClient
    {
        void Run();
    }
}
