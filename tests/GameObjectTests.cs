using Shared;
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
    }
}
