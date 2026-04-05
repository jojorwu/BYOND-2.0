using System.Numerics;
using Microsoft.Extensions.DependencyInjection;
using Shared;
using Shared.Attributes;
using Shared.Components;
using Shared.Interfaces;
using Shared.Models;
using Shared.Services;
using NUnit.Framework;

namespace Tests;

public class MovementSystem : BaseSystem
{
    public bool Ran = false;

    [Query]
    public EntityQuery<TransformComponent> Transforms { get; set; } = null!;

    public override void Tick(IEntityCommandBuffer ecb)
    {
        Ran = true;
    }

    public override void Tick(Archetype archetype, IEntityCommandBuffer ecb)
    {
        Ran = true;
        archetype.ForEach<TransformComponent>((t, id) => {
            t.Position = new Vector3(10, 0, 0);
        });
    }

    public override ValueTask TickAsync<T>(ArchetypeChunk<T> chunk, IEntityCommandBuffer ecb)
    {
        Ran = true;
        if (typeof(T) == typeof(TransformComponent))
        {
            var comps = ((ArchetypeChunk<TransformComponent>)(object)chunk).ComponentsSpan;
            foreach (var t in comps)
            {
                t.Position = new Vector3(10, 0, 0);
            }
        }
        return ValueTask.CompletedTask;
    }
}

[TestFixture]
public class EcsDoubleBufferTests
{
    private ServiceProvider _serviceProvider = null!;

    [SetUp]
    public async Task SetUp()
    {
        var services = new ServiceCollection();
        services.AddSingleton(TimeProvider.System);
        services.AddSingleton<IDiagnosticBus, Shared.Services.DiagnosticBus>();
        services.AddSingleton<IProfilingService, ProfilingService>();
        services.AddSingleton<IJobSystem, JobSystem>();
        services.AddSingleton<IComputeService, ComputeService>();
        services.AddSingleton<IObjectPool<GameObject>>(sp => new SharedPool<GameObject>(() => new GameObject()));
        services.AddSingleton<IEntityRegistry, EntityRegistry>();
        services.AddSingleton<IObjectFactory, Shared.Services.ObjectFactory>();
        services.AddSingleton<IComponentManager, ComponentManager>();
        services.AddSingleton<IArchetypeManager, ArchetypeManager>();
        services.AddSingleton<ISystemRegistry, SystemRegistry>();
        services.AddSingleton<ISystemExecutionPlanner, SystemExecutionPlanner>();
        services.AddSingleton<IComponentQueryService, ComponentQueryService>();
        services.AddLogging();
        services.AddSingleton<IObjectPool<EntityCommandBuffer>>(sp => new SharedPool<EntityCommandBuffer>(() => new EntityCommandBuffer(sp.GetRequiredService<IObjectFactory>(), sp.GetRequiredService<IComponentManager>(), sp.GetRequiredService<IJobSystem>())));
        services.AddSingleton<ISystemManager, SystemManager>();
        services.AddSingleton<ISystem, MovementSystem>();
        services.AddSingleton<MovementSystem>(sp => (MovementSystem)sp.GetServices<ISystem>().First(s => s is MovementSystem));

        _serviceProvider = services.BuildServiceProvider();

        ComponentIdRegistry.RegisterAll(typeof(TransformComponent).Assembly);
        await ((IEngineLifecycle)_serviceProvider.GetRequiredService<IComponentManager>()).PostInitializeAsync(default);
    }

    [TearDown]
    public async Task TearDown()
    {
        if (_serviceProvider != null)
            await _serviceProvider.DisposeAsync();
    }

    [Test]
    public void TransformComponent_DoubleBuffering_Works()
    {
        var transform = new TransformComponent();
        transform.Position = new Vector3(1, 0, 0);

        // Before commit, current state is still default
        Assert.That(transform.CurrentPosition, Is.EqualTo(Vector3.Zero));

        transform.CommitUpdate();

        // After commit, state is updated
        Assert.That(transform.CurrentPosition, Is.EqualTo(new Vector3(1, 0, 0)));

        transform.BeginUpdate();
        transform.Position = new Vector3(2, 0, 0);

        // While updating, current state remains 1,0,0
        Assert.That(transform.CurrentPosition, Is.EqualTo(new Vector3(1, 0, 0)));

        transform.CommitUpdate();
        Assert.That(transform.CurrentPosition, Is.EqualTo(new Vector3(2, 0, 0)));
    }

    [Test]
    public async Task SystemManager_TriggersDoubleBuffering()
    {
        var am = _serviceProvider.GetRequiredService<IArchetypeManager>();
        var sm = _serviceProvider.GetRequiredService<ISystemManager>();

        var entity = new Shared.GameObject { Id = 1 };
        var transform = new TransformComponent();
        am.AddComponent(entity, transform);

        await ((IEngineService)sm).InitializeAsync();

        Assert.That(transform.CurrentPosition, Is.EqualTo(Vector3.Zero));

        // We need to ensure that the MovementSystem is in the execution layers.
        // Re-get it from the registry.
        var sr = _serviceProvider.GetRequiredService<ISystemRegistry>();
        var system = sr.GetSystems().OfType<MovementSystem>().First();

        await sm.TickAsync();

        Assert.That(system.Ran, Is.True, "MovementSystem should have run");
        Assert.That(transform.CurrentPosition, Is.EqualTo(new Vector3(10, 0, 0)));
    }
}
