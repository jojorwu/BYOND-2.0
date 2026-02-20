using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using NUnit.Framework;
using Shared;
using Shared.Interfaces;
using Shared.Models;
using Shared.Services;
using System;
using System.Reflection;

namespace tests;

public abstract class BaseEngineTest
{
    protected ServiceProvider ServiceProvider { get; private set; } = null!;
    protected IServiceCollection Services { get; private set; } = null!;

    [SetUp]
    public virtual void Setup()
    {
        Services = new ServiceCollection();

        // Register core engine services
        Services.AddSharedEngineServices();

        // Add null loggers by default
        Services.AddSingleton<ILoggerFactory, NullLoggerFactory>();
        Services.AddSingleton(typeof(ILogger<>), typeof(NullLogger<>));

        // Mock GameState if not provided
        if (!Services.Any(d => d.ServiceType == typeof(IGameState)))
        {
            Services.AddSingleton<IGameState, GameState>();
        }

        ConfigureServices(Services);

        ServiceProvider = Services.BuildServiceProvider();
    }

    [TearDown]
    public virtual void TearDown()
    {
        ServiceProvider.Dispose();
    }

    /// <summary>
    /// Override this method to register additional services or mocks for the test.
    /// </summary>
    protected virtual void ConfigureServices(IServiceCollection services)
    {
    }

    protected T GetService<T>() where T : notnull
    {
        return ServiceProvider.GetRequiredService<T>();
    }

    protected GameObject CreateTestObject(int id = 0)
    {
        var obj = new GameObject();
        if (id != 0) obj.Id = id;
        return obj;
    }
}
