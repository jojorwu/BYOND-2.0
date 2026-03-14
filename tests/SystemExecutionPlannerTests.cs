using NUnit.Framework;
using Moq;
using Shared.Interfaces;
using Shared.Services;
using Shared.Models;
using Shared.Enums;
using System;
using System.Collections.Generic;
using System.Linq;

namespace tests
{
    [TestFixture]
    public class SystemExecutionPlannerTests
    {
        private class TestSystem : ISystem
        {
            public string Name { get; set; }
            public ExecutionPhase Phase { get; set; } = ExecutionPhase.Simulation;
            public string[] Dependencies { get; set; } = Array.Empty<string>();
            public Type[] ReadResources { get; set; } = Array.Empty<Type>();
            public Type[] WriteResources { get; set; } = Array.Empty<Type>();
            public bool Enabled => true;

            IEnumerable<string> ISystem.Dependencies => Dependencies;
            IEnumerable<Type> ISystem.ReadResources => ReadResources;
            IEnumerable<Type> ISystem.WriteResources => WriteResources;

            public void Tick(IEntityCommandBuffer ecb) { }
        }

        private interface IRes1 : IComponent { }
        private interface IRes2 : IComponent { }

        [Test]
        public void PlanExecution_ResolvesSimpleDependencies()
        {
            var s1 = new TestSystem { Name = "S1" };
            var s2 = new TestSystem { Name = "S2", Dependencies = new[] { "S1" } };
            var planner = new SystemExecutionPlanner();

            var plan = planner.PlanExecution(new[] { s1, s2 }, new[] { ExecutionPhase.Simulation });

            Assert.That(plan[0], Has.Count.EqualTo(2)); // Two layers
            Assert.That(plan[0][0][0], Is.SameAs(s1));
            Assert.That(plan[0][1][0], Is.SameAs(s2));
        }

        [Test]
        public void PlanExecution_DetectsResourceConflicts()
        {
            var s1 = new TestSystem { Name = "S1", WriteResources = new[] { typeof(IRes1) } };
            var s2 = new TestSystem { Name = "S2", ReadResources = new[] { typeof(IRes1) } };
            var planner = new SystemExecutionPlanner();

            var plan = planner.PlanExecution(new[] { s1, s2 }, new[] { ExecutionPhase.Simulation });

            // S2 depends on S1's resource, should be in different layers even without explicit dependency
            Assert.That(plan[0], Has.Count.EqualTo(2));
        }

        [Test]
        public void PlanExecution_ParallelizesNonConflictingSystems()
        {
            var s1 = new TestSystem { Name = "S1", WriteResources = new[] { typeof(IRes1) } };
            var s2 = new TestSystem { Name = "S2", WriteResources = new[] { typeof(IRes2) } };
            var planner = new SystemExecutionPlanner();

            var plan = planner.PlanExecution(new[] { s1, s2 }, new[] { ExecutionPhase.Simulation });

            // No conflict, should be in the same layer
            Assert.That(plan[0], Has.Count.EqualTo(1));
            Assert.That(plan[0][0], Has.Count.EqualTo(2));
        }
    }
}
