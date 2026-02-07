using Shared;
using NUnit.Framework;
using System;

namespace tests
{
    [TestFixture]
    public class DreamValueTests
    {
        [Test]
        public void Equality_SameValues_AreEqual()
        {
            Assert.That(new DreamValue(10f).Equals(new DreamValue(10f)), Is.True);
            Assert.That(new DreamValue("test").Equals(new DreamValue("test")), Is.True);
            Assert.That(DreamValue.Null.Equals(DreamValue.Null), Is.True);
        }

        [Test]
        public void Equality_DifferentTypes_HandleDMParity()
        {
            // Strict Equals (used by Dictionary keys)
            Assert.That(new DreamValue(0f).Equals(DreamValue.Null), Is.False);
            Assert.That(DreamValue.Null.Equals(new DreamValue(0f)), Is.False);

            // Fuzzy operator (used by VM scripts)
            Assert.That(new DreamValue(0f) == DreamValue.Null, Is.True);
            Assert.That(DreamValue.Null == new DreamValue(0f), Is.True);

            Assert.That(new DreamValue(1f) == DreamValue.Null, Is.False);
            Assert.That(new DreamValue(0f) == new DreamValue("0"), Is.False);
        }

        [Test]
        public void Equality_FloatEpsilon_Works()
        {
            // Implementation uses 0.00001f epsilon
            Assert.That(new DreamValue(1.000001f).Equals(new DreamValue(1.000002f)), Is.True);
            Assert.That(new DreamValue(1.0001f).Equals(new DreamValue(1.0002f)), Is.False);
        }

        [Test]
        public void IsFalse_WorksCorrectly()
        {
            Assert.That(DreamValue.Null.IsFalse(), Is.True);
            Assert.That(new DreamValue(0f).IsFalse(), Is.True);
            Assert.That(new DreamValue("").IsFalse(), Is.True);

            Assert.That(new DreamValue(1f).IsFalse(), Is.False);
            Assert.That(new DreamValue("test").IsFalse(), Is.False);
        }

        [Test]
        public void Arithmetic_WorksCorrectly()
        {
            var a = new DreamValue(10f);
            var b = new DreamValue(5f);

            Assert.That((a + b).AsFloat(), Is.EqualTo(15f));
            Assert.That((a - b).AsFloat(), Is.EqualTo(5f));
            Assert.That((a * b).AsFloat(), Is.EqualTo(50f));
            Assert.That((a / b).AsFloat(), Is.EqualTo(2f));
        }

        [Test]
        public void StringConcatenation_Works()
        {
            var a = new DreamValue("Hello ");
            var b = new DreamValue("World");
            var c = new DreamValue(123f);

            Assert.That((a + b).ToString(), Is.EqualTo("Hello World"));
            Assert.That((a + c).ToString(), Is.EqualTo("Hello 123"));
            Assert.That((c + b).ToString(), Is.EqualTo("123World"));
            Assert.That((a + DreamValue.Null).ToString(), Is.EqualTo("Hello "));
            Assert.That((DreamValue.Null + b).ToString(), Is.EqualTo("World"));
        }

        [Test]
        public void RelationalOperators_StringComparison_Works()
        {
            var a = new DreamValue("abc");
            var b = new DreamValue("def");
            var c = new DreamValue("abc");

            Assert.That(a < b, Is.True);
            Assert.That(b > a, Is.True);
            Assert.That(a <= c, Is.True);
            Assert.That(a >= c, Is.True);
            Assert.That(b >= a, Is.True);
        }

        [Test]
        public void ListArithmetic_Works()
        {
            var list1 = new DreamList(null);
            list1.AddValue(new DreamValue(1f));
            list1.AddValue(new DreamValue(2f));

            var list2 = new DreamList(null);
            list2.AddValue(new DreamValue(2f));
            list2.AddValue(new DreamValue(3f));

            // list1 + 3 -> [1, 2, 3]
            var sum1 = (new DreamValue(list1) + new DreamValue(3f)).GetValueAsDreamObject() as DreamList;
            Assert.That(sum1!.Values.Count, Is.EqualTo(3));
            Assert.That(sum1.Values[2].AsFloat(), Is.EqualTo(3f));

            // list1 + list2 -> [1, 2, 2, 3]
            var sum2 = (new DreamValue(list1) + new DreamValue(list2)).GetValueAsDreamObject() as DreamList;
            Assert.That(sum2!.Values.Count, Is.EqualTo(4));
            Assert.That(sum2.Values[2].AsFloat(), Is.EqualTo(2f));
            Assert.That(sum2.Values[3].AsFloat(), Is.EqualTo(3f));

            // list1 - 2 -> [1]
            var sub1 = (new DreamValue(list1) - new DreamValue(2f)).GetValueAsDreamObject() as DreamList;
            Assert.That(sub1!.Values.Count, Is.EqualTo(1));
            Assert.That(sub1.Values[0].AsFloat(), Is.EqualTo(1f));
        }

        [Test]
        public void BitwiseParity_24Bit_Test()
        {
            // BYOND uses 24-bit bitwise operations
            var largeValue = new DreamValue((float)0xFFFFFFFF); // All bits set
            var maskedValue = ~largeValue;

            // In 32-bit: ~0xFFFFFFFF = 0
            // In 24-bit: (~0xFFFFFFFF) & 0xFFFFFF = 0
            Assert.That(maskedValue.AsInt(), Is.EqualTo(0));

            var val1 = new DreamValue((float)0x123456);
            var val2 = new DreamValue((float)0xF0F0F0);

            Assert.That((val1 & val2).AsInt(), Is.EqualTo((0x123456 & 0xF0F0F0) & 0x00FFFFFF));
            Assert.That((val1 | val2).AsInt(), Is.EqualTo((0x123456 | 0xF0F0F0) & 0x00FFFFFF));
        }
    }
}
