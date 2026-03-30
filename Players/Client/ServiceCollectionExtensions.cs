using System;
using Microsoft.Extensions.DependencyInjection;
using Client.Services;
using Shared.Services;

namespace Client;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddClientServices(this IServiceCollection services)
    {
        services.AddSingleton<ISnapshotManager, SnapshotManager>();
        services.AddSingleton<IStateInterpolator, StateInterpolator>();
        services.AddSingleton<Game>();
        return services;
    }
}
