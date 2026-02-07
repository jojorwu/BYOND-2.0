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
            Assert.That(new DreamValue(0f).Equals(DreamValue.Null), Is.True);
            Assert.That(DreamValue.Null.Equals(new DreamValue(0f)), Is.True);
            Assert.That(new DreamValue(1f).Equals(DreamValue.Null), Is.False);
            Assert.That(new DreamValue(0f).Equals(new DreamValue("0")), Is.False);
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
    }
}
