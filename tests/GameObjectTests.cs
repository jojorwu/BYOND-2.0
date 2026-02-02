using Shared;
using Shared.Models;
using Shared.Services;
using Shared.Interfaces;
using Shared.Enums;
using Shared.Operations;
using NUnit.Framework;
using Core;

namespace tests
{
    [TestFixture]
    public class GameObjectTests
    {
        [Test]
        public void GetVariable_ShouldResolveFromInstance_ThenDirectType_ThenParentType()
        {
            // Arrange
            var parentType = new ObjectType(1, "/obj/parent");
            parentType.VariableNames.Add("parentProperty");
            parentType.FlattenedDefaultValues.Add("parentValue");
            parentType.VariableNames.Add("overrideProperty");
            parentType.FlattenedDefaultValues.Add("parentOverride");

            var childType = new ObjectType(2, "/obj/child") { Parent = parentType };
            childType.VariableNames.AddRange(parentType.VariableNames);
            childType.VariableNames.Add("childProperty");

            childType.FlattenedDefaultValues.AddRange(parentType.FlattenedDefaultValues);
            // override property in child
            childType.FlattenedDefaultValues[1] = "childOverride";
            childType.FlattenedDefaultValues.Add("childValue");

            var gameObject = new GameObject(childType);

            // Act & Assert
            // Should get from direct type (child)
            Assert.That(gameObject.GetVariable("childProperty").ToString(), Is.EqualTo("childValue"));
            Assert.That(gameObject.GetVariable("overrideProperty").ToString(), Is.EqualTo("childOverride"));

            // Set override in instance
            gameObject.SetVariable("overrideProperty", new DreamValue("instanceOverride"));
            Assert.That(gameObject.GetVariable("overrideProperty").ToString(), Is.EqualTo("instanceOverride"));

            // Should get from parent type
            Assert.That(gameObject.GetVariable("parentProperty").ToString(), Is.EqualTo("parentValue"));

            // Should not find non-existent property
            Assert.That(gameObject.GetVariable("nonExistentProperty"), Is.EqualTo(DreamValue.Null));
        }

        [Test]
        public void IGameObject_InterfaceMethods_WorkCorrectly()
        {
            var type = new ObjectType(1, "/obj");
            IGameObject obj = new GameObject(type, 1, 2, 3);

            Assert.That(obj.X, Is.EqualTo(1));
            Assert.That(obj.Y, Is.EqualTo(2));
            Assert.That(obj.Z, Is.EqualTo(3));

            obj.SetPosition(10, 20, 30);
            Assert.That(obj.X, Is.EqualTo(10));
            Assert.That(obj.Y, Is.EqualTo(20));
            Assert.That(obj.Z, Is.EqualTo(30));

            obj.X = 5;
            Assert.That(obj.X, Is.EqualTo(5));
        }

        [Test]
        public void GetVariableByIndex_WorksCorrectly()
        {
            var type = new ObjectType(1, "/obj");
            type.VariableNames.Add("test");
            type.FlattenedDefaultValues.Add(100f);

            var obj = new GameObject(type);

            Assert.That(obj.GetVariable(0).AsFloat(), Is.EqualTo(100f));

            obj.SetVariable(0, 200f);
            Assert.That(obj.GetVariable("test").AsFloat(), Is.EqualTo(200f));
        }
    }
}
