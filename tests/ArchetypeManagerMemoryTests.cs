using NUnit.Framework;
using Moq;
using Shared.Interfaces;
using Shared.Services;
using Shared.Models;
using Shared;
using Microsoft.Extensions.Logging.Abstractions;
using System;
using System.Collections.Generic;
using System.Linq;

namespace tests
{
    [TestFixture]
    public class ArchetypeManagerMemoryTests
    {
        private class Comp1 : BaseComponent { }
        private class Comp2 : BaseComponent { }

        [Test]
        public void ArchetypeManager_ReconstructsComponentsCorrectly()
        {
            var am = new ArchetypeManager(NullLogger<ArchetypeManager>.Instance);
            var obj = new GameObject { Id = 1 };

            var c1 = new Comp1();
            am.AddComponent(obj, c1);

            var c2 = new Comp2();
            am.AddComponent(obj, c2);

            var comps = am.GetAllComponents(1).ToList();
            Assert.That(comps, Has.Count.EqualTo(2));
            Assert.That(comps, Contains.Item(c1));
            Assert.That(comps, Contains.Item(c2));

            am.RemoveComponent<Comp1>(obj);
            comps = am.GetAllComponents(1).ToList();
            Assert.That(comps, Has.Count.EqualTo(1));
            Assert.That(comps[0], Is.InstanceOf<Comp2>());
        }

        [Test]
        public void ArchetypeManager_HandlesLargeEntityCount()
        {
            var am = new ArchetypeManager(NullLogger<ArchetypeManager>.Instance);
            int count = 10000;
            var objects = new List<GameObject>();

            for (int i = 0; i < count; i++)
            {
                var obj = new GameObject { Id = i + 100 };
                am.AddComponent(obj, new Comp1());
                objects.Add(obj);
            }

            Assert.That(am.GetDiagnosticInfo()["EntityCount"], Is.EqualTo(count));

            // Verify we can still access components for a random object
            var randomObj = objects[5000];
            Assert.That(am.GetComponent<Comp1>(randomObj.Id), Is.Not.Null);
        }
    }
}
