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
        /// <summary>
        /// Registers all client-specific services, including the game engine, asset caches, and shaders.
        /// </summary>
        public static IServiceCollection AddClientServices(this IServiceCollection services)
        {
            return services
                .AddSharedEngineServices()
                .AddClientCoreServices()
                .AddClientAssetServices();
        }

        private static IServiceCollection AddClientCoreServices(this IServiceCollection services)
        {
            services.AddSingleton<Game>();
            services.AddSingleton<IClient>(p => p.GetRequiredService<Game>());

            services.AddSingleton<ClientApplication>();
            services.AddHostedService(p => p.GetRequiredService<ClientApplication>());

            return services;
        }

        private static IServiceCollection AddClientAssetServices(this IServiceCollection services)
        {
            services.AddEngineService<TextureCache>();
            services.AddEngineService<CSharpShaderManager>();
            services.AddEngineService<DmiCache>();
            services.AddEngineService<IconCache>();

            services.AddSingleton<ISoundSystem, MockSoundSystem>();

            return services;
        }
    }

    public interface IClient
    {
        void Run();
    }
}
