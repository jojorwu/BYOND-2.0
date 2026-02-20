using Microsoft.Extensions.Logging.Abstractions;
using NUnit.Framework;
using Shared;
using Shared.Interfaces;
using Shared.Models;
using Shared.Services;
using System;
using System.Collections.Generic;
using System.Linq;

namespace tests
{
    [TestFixture]
    public class ArchetypePerformanceTests
    {
        [Test]
        public void ArchetypeManager_Lookup_IsFastAndCorrect()
        {
            var manager = new ArchetypeManager(NullLogger<ArchetypeManager>.Instance);
            var objects = new List<GameObject>();

            // Create objects with different component combinations
            for (int i = 0; i < 100; i++)
            {
                var obj = new GameObject();
                obj.Id = i + 1;
                manager.AddEntity(obj);

                if (i % 2 == 0) manager.AddComponent(obj, new TestComponent1());
                if (i % 3 == 0) manager.AddComponent(obj, new TestComponent2());

                objects.Add(obj);
            }

            // Verify counts
            var c1Count = manager.GetComponents<TestComponent1>().Count();
            var c2Count = manager.GetComponents<TestComponent2>().Count();

            Assert.That(c1Count, Is.EqualTo(50));
            Assert.That(c2Count, Is.EqualTo(34));

            // Verify overlap (multi-component query simulation)
            var overlapCount = 0;
            foreach (var obj in objects)
            {
                if (manager.GetComponent<TestComponent1>(obj.Id) != null &&
                    manager.GetComponent<TestComponent2>(obj.Id) != null)
                {
                    overlapCount++;
                }
            }

            // 0, 6, 12, 18... (multiples of 6 up to 96) = 17 items
            Assert.That(overlapCount, Is.EqualTo(17));
        }

        private class TestComponent1 : IComponent
        {
            public IGameObject? Owner { get; set; }
            public bool Enabled { get; set; } = true;
            public void Initialize() { }
            public void Shutdown() { }
            public void OnMessage(IComponentMessage message) { }
            public void SendMessage(IComponentMessage message) { }
        }

        private class TestComponent2 : IComponent
        {
            public IGameObject? Owner { get; set; }
            public bool Enabled { get; set; } = true;
            public void Initialize() { }
            public void Shutdown() { }
            public void OnMessage(IComponentMessage message) { }
            public void SendMessage(IComponentMessage message) { }
        }
    }
}
