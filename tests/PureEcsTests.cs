using NUnit.Framework;
using Shared.Interfaces;
using Shared.Models;
using Shared.Services;
using Shared;
using System.Collections.Generic;
using System;

namespace Tests.ECS;

public struct TestDataComponent : IDataComponent
{
    public int Value;
}

[TestFixture]
public class PureEcsTests
{
    private ArchetypeManager _archetypeManager;
    private ComponentQueryService _queryService;
    private MockDiagnosticBus _diagnosticBus;

    [SetUp]
    public void Setup()
    {
        _diagnosticBus = new MockDiagnosticBus();
        _archetypeManager = new ArchetypeManager(new Microsoft.Extensions.Logging.Abstractions.NullLogger<ArchetypeManager>(), _diagnosticBus);
        var componentManager = new Shared.Services.ComponentManager(_archetypeManager);
        _queryService = new ComponentQueryService(componentManager, _archetypeManager, TimeProvider.System, _diagnosticBus, null);
    }

    [TearDown]
    public void TearDown()
    {
        _queryService.Dispose();
    }

    [Test]
    public void Archetype_CanStoreStructComponents()
    {
        var obj = new GameObject();
        obj.Initialize(null!, 0, 0, 0);

        _archetypeManager.SetDataComponent(obj, new TestDataComponent { Value = 42 });

        Assert.That(obj.GetDataComponent<TestDataComponent>().Value, Is.EqualTo(42));

        var arch = obj.Archetype as Archetype;
        Assert.That(arch, Is.Not.Null);
        Assert.That(arch.Signature.Types.Contains(typeof(TestDataComponent)), Is.True);
    }

    [Test]
    public void EntityCommandBuffer_WorksWithStreaming()
    {
        var diagnosticBus = new Shared.Services.MockDiagnosticBus();
        var jobSystem = new Shared.Services.JobSystem(new Microsoft.Extensions.Logging.Abstractions.NullLogger<JobSystem>(), TimeProvider.System, diagnosticBus);
        var componentManager = new Shared.Services.ComponentManager(_archetypeManager);
        var factory = new Shared.Services.ObjectFactory(null!);
        var ecb = new EntityCommandBuffer(factory, componentManager, jobSystem);

        var obj = new GameObject();
        obj.SetComponentManager(componentManager);
        obj.Initialize(null!, 0, 0, 0);
        _archetypeManager.AddEntity(obj);

        // Ensure IDs are registered
        ComponentIdRegistry.Register<TestDataComponent>();

        ecb.SetDataComponent(obj, new TestDataComponent { Value = 100 });
        ecb.Playback();

        var val = obj.GetDataComponent<TestDataComponent>().Value;
        Assert.That(val, Is.EqualTo(100));
    }

    [Test]
    public void SystemManager_CanProcessStructComponents()
    {
        var diagnosticBus = new Shared.Services.MockDiagnosticBus();
        var profilingService = new Shared.Services.ProfilingService();
        var jobSystem = new Shared.Services.JobSystem(new Microsoft.Extensions.Logging.Abstractions.NullLogger<JobSystem>(), TimeProvider.System, diagnosticBus);
        var componentManager = new Shared.Services.ComponentManager(_archetypeManager);
        var ecbPool = new Shared.Services.ObjectPool<EntityCommandBuffer>(() => new EntityCommandBuffer(new Shared.Services.ObjectFactory(null!), componentManager, jobSystem));
        var registry = new Shared.Services.SystemRegistry();
        var planner = new Shared.Services.SystemExecutionPlanner();
        var loggerFactory = new Microsoft.Extensions.Logging.Abstractions.NullLoggerFactory();

        var system = new TestDataSystem();
        var systems = new List<ISystem> { system };

        // Ensure IDs are registered
        ComponentIdRegistry.Register<TestDataComponent>();

        var systemManager = new SystemManager(registry, planner, profilingService, jobSystem, ecbPool, _archetypeManager, systems, loggerFactory, _queryService, diagnosticBus);
        ((IEngineService)systemManager).InitializeAsync().Wait();

        var obj = new GameObject();
        obj.Initialize(null!, 0, 0, 0);
        _archetypeManager.SetDataComponent(obj, new TestDataComponent { Value = 10 });

        // Force rebuild of query to include the new archetype
        _queryService.GetQuery([typeof(TestDataComponent)]);

        systemManager.TickAsync().Wait();

        Assert.That(obj.GetDataComponent<TestDataComponent>().Value, Is.EqualTo(11));
    }
}

public class TestDataSystem : Shared.Models.BaseSystem
{
    [Shared.Attributes.Query]
    private EntityQuery<TestDataComponent> _query;

    public override ValueTask TickAsync<T>(ArchetypeChunk<T> chunk, IEntityCommandBuffer ecb)
    {
        if (typeof(T) == typeof(TestDataComponent))
        {
            var components = ((ArchetypeChunk<TestDataComponent>)(object)chunk).ComponentsMutableSpan;
            for (int i = 0; i < components.Length; i++)
            {
                components[i].Value++;
            }
        }
        return ValueTask.CompletedTask;
    }

    public override void Tick(IEntityCommandBuffer ecb) { }
}
