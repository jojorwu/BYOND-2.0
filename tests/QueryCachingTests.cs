using NUnit.Framework;
using Shared;
using Shared.Interfaces;
using Shared.Services;
using System;
using System.Collections.Generic;
using System.Linq;

namespace tests
{
    [TestFixture]
    public class QueryCachingTests
    {
        private class TestComponent1 : IComponent
        {
            public IGameObject? Owner { get; set; }
            public bool Enabled { get; set; } = true;
            public void SendMessage(IComponentMessage message) { }
        }

        private class TestComponent2 : IComponent
        {
            public IGameObject? Owner { get; set; }
            public bool Enabled { get; set; } = true;
            public void SendMessage(IComponentMessage message) { }
        }

        private IComponentManager _componentManager;
        private ComponentQueryService _queryService;
        private IObjectFactory _objectFactory;

        [SetUp]
        public void SetUp()
        {
            var archetypeManager = new ArchetypeManager();
            _componentManager = new ComponentManager(archetypeManager);
            _queryService = new ComponentQueryService(_componentManager);
            var pool = new SharedPool<GameObject>(() => new GameObject());
            _objectFactory = new ObjectFactory(pool, _componentManager);
        }

        [Test]
        public void QueryService_CachesResults()
        {
            var type = new ObjectType(1, "/obj");
            var obj1 = _objectFactory.Create(type);
            var obj2 = _objectFactory.Create(type);

            obj1.AddComponent(new TestComponent1());
            obj1.AddComponent(new TestComponent2());
            obj2.AddComponent(new TestComponent1());

            // First query (cold)
            var results = _queryService.Query(typeof(TestComponent1), typeof(TestComponent2)).ToList();
            Assert.That(results.Count, Is.EqualTo(1));
            Assert.That(results[0], Is.SameAs(obj1));

            // Second query (hot - from cache)
            var results2 = _queryService.Query(typeof(TestComponent1), typeof(TestComponent2)).ToList();
            Assert.That(results2.Count, Is.EqualTo(1));
            Assert.That(results2[0], Is.SameAs(obj1));
        }

        [Test]
        public void QueryService_UpdatesCache_OnComponentAdded()
        {
            var type = new ObjectType(1, "/obj");
            var obj1 = _objectFactory.Create(type);

            // Initial query to prime cache
            _queryService.Query(typeof(TestComponent1), typeof(TestComponent2));

            obj1.AddComponent(new TestComponent1());
            obj1.AddComponent(new TestComponent2());

            // Query should now include obj1
            var results = _queryService.Query(typeof(TestComponent1), typeof(TestComponent2)).ToList();
            Assert.That(results.Count, Is.EqualTo(1));
            Assert.That(results[0], Is.SameAs(obj1));
        }

        [Test]
        public void QueryService_UpdatesCache_OnComponentRemoved()
        {
            var type = new ObjectType(1, "/obj");
            var obj1 = _objectFactory.Create(type);

            obj1.AddComponent(new TestComponent1());
            obj1.AddComponent(new TestComponent2());

            // Prime cache
            var results = _queryService.Query(typeof(TestComponent1), typeof(TestComponent2)).ToList();
            Assert.That(results.Count, Is.EqualTo(1));

            obj1.RemoveComponent<TestComponent1>();

            // Query should now be empty
            var results2 = _queryService.Query(typeof(TestComponent1), typeof(TestComponent2)).ToList();
            Assert.That(results2.Count, Is.EqualTo(0));
        }
    }
}
