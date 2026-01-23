using Shared;
using NUnit.Framework;
using Core;

namespace tests
{
    [TestFixture]
    public class GameObjectTests
    {
        [Test]
        public void GetProperty_ShouldResolveFromInstance_ThenDirectType_ThenParentType()
        {
            // Arrange
            var parentType = new ObjectType(1, "/obj/parent");
            parentType.Variables = new List<object> { "parentValue", "parentOverride" };
            parentType.VariableNameIds = new Dictionary<string, int> { { "parentProperty", 0 }, { "overrideProperty", 1 } };

            var childType = new ObjectType(2, "/obj/child") { Parent = parentType };
            childType.Variables = new List<object> { "parentValue", "childOverride", "childValue" };
            childType.VariableNameIds = new Dictionary<string, int> { { "parentProperty", 0 }, { "overrideProperty", 1 }, { "childProperty", 2 } };

            var gameObject = new GameObject(childType);
            gameObject.Properties["instanceProperty"] = "instanceValue";
            gameObject.Properties["overrideProperty"] = "instanceOverride";

            // Act & Assert
            // Should get from instance
            Assert.That(gameObject.GetProperty<string>("instanceProperty"), Is.EqualTo("instanceValue"));
            Assert.That(gameObject.GetProperty<string>("overrideProperty"), Is.EqualTo("instanceOverride"));

            // Should get from direct type (child)
            Assert.That(gameObject.GetProperty<string>("childProperty"), Is.EqualTo("childValue"));

            // Should get from parent type
            Assert.That(gameObject.GetProperty<string>("parentProperty"), Is.EqualTo("parentValue"));

            // Should not find non-existent property
            Assert.That(gameObject.GetProperty<string>("nonExistentProperty"), Is.Null);
        }
    }
}
