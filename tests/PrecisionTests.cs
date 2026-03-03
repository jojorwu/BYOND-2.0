using Shared;
using NUnit.Framework;
using System;

namespace tests
{
    [TestFixture]
    public class PrecisionTests
    {
        [Test]
        public void LargeId_Precision_Test()
        {
            // long.MaxValue is 2^63 - 1.
            // Double starts losing integer precision around 2^53.
            long hugeId = 9007199254740993L; // 2^53 + 1

            var val = DreamValue.CreateObjectIdReference(hugeId);
            Assert.That(val.ObjectId, Is.EqualTo(hugeId));

            // Verify that bitwise operations on huge values are exact
            var val1 = new DreamValue(0x1234567890ABCDEFL);
            var val2 = new DreamValue(unchecked((long)0xF0F0F0F0F0F0F0F0UL));

            // Should be exact using longValue path
            var result = val1 & val2;
            long expected = 0x1234567890ABCDEFL & unchecked((long)0xF0F0F0F0F0F0F0F0UL);

            // DreamValue arithmetic currently converts to double, which is expected for floats
            // but bitwise should stay precise using RawLong.
            Assert.That(result.RawLong, Is.EqualTo(expected));
        }

        [Test]
        public void LargeCoordinates_Map_Test()
        {
            var map = new Map();
            var turfType = new ObjectType(1, "/turf");
            var turf = new Turf(turfType, 1000000000L, 2000000000L, 0);

            map.SetTurf(1000000000L, 2000000000L, 0, turf);

            var retrieved = map.GetTurf(1000000000L, 2000000000L, 0);
            Assert.That(retrieved, Is.EqualTo(turf));
            Assert.That(((GameObject)retrieved!).X, Is.EqualTo(1000000000L));
            Assert.That(((GameObject)retrieved).Y, Is.EqualTo(2000000000L));
        }
    }
}
