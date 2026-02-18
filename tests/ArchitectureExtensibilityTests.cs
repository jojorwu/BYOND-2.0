using NUnit.Framework;
using Microsoft.Extensions.DependencyInjection;
using Shared.Attributes;
using Shared.Interfaces;
using Shared.Models;
using Shared.Services;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace tests
{
    [TestFixture]
    public class ArchitectureExtensibilityTests
    {
        [System("TestSystem", Group = "TestGroup", Priority = 10)]
        [Resource(typeof(string), ResourceAccess.Read)]
        [Resource(typeof(int), ResourceAccess.Write)]
        public class TestSystem : BaseSystem
        {
            public bool Ticked { get; private set; }
            public override void Tick(IEntityCommandBuffer ecb)
            {
                Ticked = true;
            }
        }

        public class TestModule : BaseModule
        {
            public override void RegisterServices(IServiceCollection services)
            {
                services.AddSystem<TestSystem>();
            }
        }

        [Test]
        public async Task Module_RegistersSystemsAndServices()
        {
            var services = new ServiceCollection();
            services.AddLogging();
            services.AddSharedEngineServices();
            services.AddEngineModule<TestModule>();

            var provider = services.BuildServiceProvider();
            var systemManager = provider.GetRequiredService<ISystemManager>();
            var registry = provider.GetRequiredService<ISystemRegistry>();

            var testSystem = provider.GetRequiredService<ISystem>() as TestSystem;
            Assert.That(testSystem, Is.Not.Null);
            Assert.That(testSystem.Name, Is.EqualTo("TestSystem"));
            Assert.That(testSystem.Group, Is.EqualTo("TestGroup"));
            Assert.That(testSystem.Priority, Is.EqualTo(10));
            Assert.That(testSystem.ReadResources.Contains(typeof(string)), Is.True);
            Assert.That(testSystem.WriteResources.Contains(typeof(int)), Is.True);

            // Verify it was automatically registered in SystemRegistry via SystemManager constructor
            var registeredSystem = registry.GetSystem("TestSystem");
            Assert.That(registeredSystem, Is.SameAs(testSystem));

            // Verify Tick
            await systemManager.TickAsync();
            Assert.That(testSystem.Ticked, Is.True);
        }
    }
}
