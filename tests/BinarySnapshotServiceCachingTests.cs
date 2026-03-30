using NUnit.Framework;
using Shared;
using Shared.Services;
using Shared.Interfaces;
using System;
using System.Collections.Generic;
using System.Linq;

namespace tests
{
    [TestFixture]
    public class BinarySnapshotServiceCachingTests
    {
        [Test]
        public void SerializeTo_UsesCache()
        {
            var service = new BinarySnapshotService(new BitPackedSnapshotSerializer());
            var type = new ObjectType(1, "/obj");
            type.FinalizeVariables();

            var obj = new GameObject(type) { Id = 1 };
            obj.SetVariableDirect(0, new DreamValue(10.0));

            var buffer1 = new byte[1024];
            var objects = new[] { obj };

            // First serialization - populates cache
            int bytes1 = service.SerializeTo(buffer1, objects, null, out bool truncated1);
            Assert.That(truncated1, Is.False);

            // Modify object - cache should be bypassed if version increases (handled by version check in cache)
            // But if we DON'T modify it, second serialization should produce EXACTLY the same bytes
            var buffer2 = new byte[1024];
            int bytes2 = service.SerializeTo(buffer2, objects, null, out bool truncated2);

            Assert.That(bytes2, Is.EqualTo(bytes1));
            Assert.That(buffer2.Take(bytes2), Is.EquivalentTo(buffer1.Take(bytes1)));

            // Shrink cache
            service.Shrink();

            // Third serialization - should re-populate cache
            var buffer3 = new byte[1024];
            int bytes3 = service.SerializeTo(buffer3, objects, null, out bool truncated3);
            Assert.That(bytes3, Is.EqualTo(bytes1));
        }

        [Test]
        public void SerializeTo_HandlesCacheInvalidation()
        {
            var service = new BinarySnapshotService(new BitPackedSnapshotSerializer());
            var type = new ObjectType(1, "/obj");
            type.FinalizeVariables();

            var obj = new GameObject(type) { Id = 1 };

            var buffer1 = new byte[1024];
            service.SerializeTo(buffer1, new[] { obj }, null, out _);

            // Increase version
            obj.X = 10;

            var buffer2 = new byte[1024];
            int bytes2 = service.SerializeTo(buffer2, new[] { obj }, null, out _);

            // New serialization should have different data (at least X is different)
            Assert.That(buffer2.Take(bytes2), Is.Not.EquivalentTo(buffer1.Take(10)));
        }
    }
}
