using System;
using Microsoft.Extensions.DependencyInjection;
using Shared.Services;
using Shared.Interfaces;
using Client.Networking.Handlers;

namespace Client;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddClientServices(this IServiceCollection services)
    {
        services.AddSharedEngineServices();
        services.AddSingleton<Game>();

        services.AddSingleton<IPacketHandler, BitPackedDeltaHandler>();

        return services;
    }
}
