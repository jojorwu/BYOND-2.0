using NUnit.Framework;
using Shared;
using Shared.Interfaces;
using Shared.Messaging;
using Shared.Services;
using Moq;
using System;
using System.Collections.Generic;
using System.Linq;

namespace tests
{
    [TestFixture]
    public class MessagingTests
    {
        private class TestMessage : IComponentMessage
        {
            public int Value { get; set; }
        }

        private class TestComponent1 : IComponent
        {
            public IGameObject? Owner { get; set; }
            public bool Enabled { get; set; } = true;
            public int ReceivedMessages { get; private set; }

            public void SendMessage(IComponentMessage message) { }
            public void OnMessage(IComponentMessage message)
            {
                if (message is TestMessage)
                    ReceivedMessages++;
            }
        }

        private class TestComponent2 : IComponent
        {
            public IGameObject? Owner { get; set; }
            public bool Enabled { get; set; } = true;
            public int ReceivedMessages { get; private set; }

            public void SendMessage(IComponentMessage message) { }
            public void OnMessage(IComponentMessage message)
            {
                if (message is TestMessage)
                    ReceivedMessages++;
            }
        }

        [Test]
        public void GameObject_SendMessage_DispatchesToAllComponents()
        {
            var archetypeManagerMock = new Mock<IArchetypeManager>();
            var componentManager = new ComponentManager(archetypeManagerMock.Object);
            var obj = new GameObject(new ObjectType(1, "/obj"));
            obj.SetComponentManager(componentManager);

            var comp1 = new TestComponent1();
            var comp2 = new TestComponent2();

            obj.AddComponent(comp1);
            obj.AddComponent(comp2);

            var msg = new TestMessage();
            obj.SendMessage(msg);

            Assert.That(comp1.ReceivedMessages, Is.EqualTo(1));
            Assert.That(comp2.ReceivedMessages, Is.EqualTo(1));
        }

        [Test]
        public void GameObject_SendMessage_RespectsEnabledFlag()
        {
            var archetypeManagerMock = new Mock<IArchetypeManager>();
            var componentManager = new ComponentManager(archetypeManagerMock.Object);
            var obj = new GameObject(new ObjectType(1, "/obj"));
            obj.SetComponentManager(componentManager);

            var comp = new TestComponent1 { Enabled = false };
            obj.AddComponent(comp);

            obj.SendMessage(new TestMessage());

            Assert.That(comp.ReceivedMessages, Is.EqualTo(0));
        }

        [Test]
        public void GameObject_RemoveComponent_UpdatesCache()
        {
            var archetypeManagerMock = new Mock<IArchetypeManager>();
            var componentManager = new ComponentManager(archetypeManagerMock.Object);
            var obj = new GameObject(new ObjectType(1, "/obj"));
            obj.SetComponentManager(componentManager);

            var comp = new TestComponent1();
            obj.AddComponent(comp);

            obj.RemoveComponent<TestComponent1>();

            obj.SendMessage(new TestMessage());

            Assert.That(comp.ReceivedMessages, Is.EqualTo(0));
            Assert.That(obj.GetComponents().Count(), Is.EqualTo(0));
        }

        [Test]
        public void GameObject_Reset_ClearsCache()
        {
            var archetypeManagerMock = new Mock<IArchetypeManager>();
            var componentManager = new ComponentManager(archetypeManagerMock.Object);
            var obj = new GameObject(new ObjectType(1, "/obj"));
            obj.SetComponentManager(componentManager);

            var comp = new TestComponent1();
            obj.AddComponent(comp);

            obj.Reset();

            Assert.That(obj.GetComponents().Count(), Is.EqualTo(0));
        }

        [Test]
        public void GameObject_AddComponent_ReplacesExistingOfType()
        {
            var archetypeManagerMock = new Mock<IArchetypeManager>();
            var componentManager = new ComponentManager(archetypeManagerMock.Object);
            var obj = new GameObject(new ObjectType(1, "/obj"));
            obj.SetComponentManager(componentManager);

            var comp1 = new TestComponent1();
            var comp2 = new TestComponent1();

            obj.AddComponent(comp1);
            obj.AddComponent(comp2);

            Assert.That(obj.GetComponents().Count(), Is.EqualTo(1));
            Assert.That(obj.GetComponents().First(), Is.SameAs(comp2));
        }
    }
}
