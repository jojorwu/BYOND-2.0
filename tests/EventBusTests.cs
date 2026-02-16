using NUnit.Framework;
using Shared.Services;
using Shared.Messaging;
using System;

namespace tests
{
    [TestFixture]
    public class EventBusTests
    {
        private class TestEvent1 { }
        private class TestEvent2 { }

        [Test]
        public void EventBus_ClearT_RemovesOnlySpecificHandlers()
        {
            var bus = new EventBus();
            int callCount1 = 0;
            int callCount2 = 0;

            bus.Subscribe<TestEvent1>(e => callCount1++);
            bus.Subscribe<TestEvent2>(e => callCount2++);

            bus.Clear<TestEvent1>();

            bus.Publish(new TestEvent1());
            bus.Publish(new TestEvent2());

            Assert.That(callCount1, Is.EqualTo(0));
            Assert.That(callCount2, Is.EqualTo(1));
        }

        [Test]
        public void EventBus_Clear_RemovesAllHandlers()
        {
            var bus = new EventBus();
            int callCount1 = 0;
            int callCount2 = 0;

            bus.Subscribe<TestEvent1>(e => callCount1++);
            bus.Subscribe<TestEvent2>(e => callCount2++);

            bus.Clear();

            bus.Publish(new TestEvent1());
            bus.Publish(new TestEvent2());

            Assert.That(callCount1, Is.EqualTo(0));
            Assert.That(callCount2, Is.EqualTo(0));
        }
    }
}
