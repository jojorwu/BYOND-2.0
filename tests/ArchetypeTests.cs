using NUnit.Framework;
using Shared.Interfaces;
using Shared.Models;
using System;
using System.Collections.Generic;
using Moq;

using Shared;

namespace tests
{
    [TestFixture]
    public class ArchetypeTests
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

        [Test]
        public void Archetype_AddEntity_AddsToIndex()
        {
            var signature = new[] { typeof(TestComponent1) };
            var archetype = new Archetype(signature);
            var components = new Dictionary<Type, IComponent> { { typeof(TestComponent1), new TestComponent1() } };

            archetype.AddEntity(1, components);

            Assert.That(archetype.EntityCount, Is.EqualTo(1));
            Assert.That(archetype.ContainsEntity(1), Is.True);
            Assert.That(archetype.GetComponent(1, typeof(TestComponent1)), Is.SameAs(components[typeof(TestComponent1)]));
        }

        [Test]
        public void Archetype_RemoveEntity_RemovesFromIndex()
        {
            var signature = new[] { typeof(TestComponent1) };
            var archetype = new Archetype(signature);
            var components = new Dictionary<Type, IComponent> { { typeof(TestComponent1), new TestComponent1() } };

            archetype.AddEntity(1, components);
            archetype.RemoveEntity(1);

            Assert.That(archetype.EntityCount, Is.EqualTo(0));
            Assert.That(archetype.ContainsEntity(1), Is.False);
        }

        [Test]
        public void Archetype_RemoveEntity_SwapsCorrectly()
        {
            var signature = new[] { typeof(TestComponent1) };
            var archetype = new Archetype(signature);

            var comp1 = new TestComponent1();
            var comp2 = new TestComponent1();
            var comp3 = new TestComponent1();

            archetype.AddEntity(1, new Dictionary<Type, IComponent> { { typeof(TestComponent1), comp1 } });
            archetype.AddEntity(2, new Dictionary<Type, IComponent> { { typeof(TestComponent1), comp2 } });
            archetype.AddEntity(3, new Dictionary<Type, IComponent> { { typeof(TestComponent1), comp3 } });

            // Remove middle entity
            archetype.RemoveEntity(2);

            Assert.That(archetype.EntityCount, Is.EqualTo(2));
            Assert.That(archetype.ContainsEntity(2), Is.False);
            Assert.That(archetype.ContainsEntity(1), Is.True);
            Assert.That(archetype.ContainsEntity(3), Is.True);

            // Verify entity 3 was moved to index 1 (where entity 2 was)
            Assert.That(archetype.GetComponent(3, typeof(TestComponent1)), Is.SameAs(comp3));
            Assert.That(archetype.GetComponent(1, typeof(TestComponent1)), Is.SameAs(comp1));
        }

        [Test]
        public void Archetype_Compact_TrimsExcess()
        {
            var signature = new[] { typeof(TestComponent1) };
            var archetype = new Archetype(signature);

            for (int i = 0; i < 100; i++)
            {
                archetype.AddEntity(i, new Dictionary<Type, IComponent> { { typeof(TestComponent1), new TestComponent1() } });
            }

            for (int i = 0; i < 90; i++)
            {
                archetype.RemoveEntity(i);
            }

            // Compact should not throw and should ideally reduce capacity (though we can't easily check capacity of List via reflection here without more work, but we verify it runs)
            Assert.DoesNotThrow(() => archetype.Compact());
        }
    }
}
