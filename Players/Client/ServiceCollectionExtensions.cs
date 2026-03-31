using System;
using System.Collections.Generic;
using Microsoft.Extensions.DependencyInjection;
using Shared.Services;
using Shared.Interfaces;
using Client.Networking.Handlers;
using Client.Services;

namespace Client;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddClientServices(this IServiceCollection services)
    {
        services.AddSharedEngineServices();
        services.AddSingleton<Game>();
        services.AddSingleton<IClientObjectManager, ClientObjectManager>();

        services.AddSingleton<IPacketHandler, BitPackedDeltaHandler>();
        services.AddSingleton<IPacketHandler, SoundHandler>();
        services.AddSingleton<IPacketHandler, StopSoundHandler>();
        services.AddSingleton<IPacketHandler, SyncCVarsHandler>();

        return services;
    }
}
