using System;
using System.Collections.Generic;
using Microsoft.Extensions.DependencyInjection;
using Shared.Services;
using Shared.Interfaces;
using Client.Services;

namespace Client;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddClientServices(this IServiceCollection services)
    {
        services.AddSharedEngineServices();
        services.AddSingleton<Game>();
        services.AddSingleton<IClientObjectManager, ClientObjectManager>();

        return services;
    }
}
