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
    public class NetworkingPerformanceTests
    {
        [Test]
        public void BinarySnapshotService_Serialization_MinimizesAllocations()
        {
            var interner = new StringInterner();
            var service = new BinarySnapshotService(interner);

            var type = new ObjectType(1, "/obj/test");
            for (int i = 0; i < 20; i++)
            {
                type.VariableNames.Add($"var{i}");
                type.FlattenedDefaultValues.Add(new DreamValue(i));
            }
            type.FinalizeVariables();

            var objects = new List<GameObject>();
            for (int i = 0; i < 100; i++)
            {
                var obj = new GameObject(type);
                obj.Id = i + 1;
                objects.Add(obj);
            }

            // Warm up
            service.Serialize(objects);

            long startAlloc = GC.GetAllocatedBytesForCurrentThread();

            // Perform multiple serializations
            for (int i = 0; i < 10; i++)
            {
                byte[] data = service.Serialize(objects);
                Assert.That(data.Length, Is.GreaterThan(0));
            }

            long endAlloc = GC.GetAllocatedBytesForCurrentThread();
            long totalAlloc = endAlloc - startAlloc;

            // 100 objects * 20 variables * 10 iterations = 20,000 variable accesses.
            // Previously, each object serialization created a Dictionary.
            // 1000 Dictionaries would have been created.
            // Now, it should be much lower.

            Console.WriteLine($"Total allocations for 10 serializations of 100 objects: {totalAlloc} bytes");

            // We expect allocations to be relatively low (mostly the resulting byte arrays)
            // Each serialization produces ~10KB-20KB. 10 iterations = 200KB.
            // Allow some overhead for IEnumerable etc.
            Assert.That(totalAlloc, Is.LessThan(2000000)); // 2MB is a safe upper bound for 1000 objects total, but it should be way lower.
        }

        [Test]
        public void TimerService_Tick_DoesNotAllocateWhenEmpty()
        {
            var service = new TimerService();

            // Warm up
            service.Tick();

            long startAlloc = GC.GetAllocatedBytesForCurrentThread();
            for (int i = 0; i < 1000; i++)
            {
                service.Tick();
            }
            long endAlloc = GC.GetAllocatedBytesForCurrentThread();

            Assert.That(endAlloc - startAlloc, Is.LessThan(1000)); // Should be near zero
        }
    }
}
