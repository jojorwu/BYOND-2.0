using NUnit.Framework;
using Core;

namespace tests
{
    [TestFixture]
    public class ObjectTypeTests
    {
        [Test]
        public void GetProperty_WhenInstancePropertyExists_ReturnsInstanceValue()
        {
            // Arrange
            var objectType = new ObjectType("test");
            objectType.DefaultProperties["SpritePath"] = "default.png";
            var gameObject = new GameObject(objectType);
            gameObject.Properties["SpritePath"] = "instance.png";

            // Act
            var spritePath = gameObject.GetProperty<string>("SpritePath");

            // Assert
            Assert.That(spritePath, Is.EqualTo("instance.png"));
        }

        [Test]
        public void GetProperty_WhenInstancePropertyDoesNotExist_ReturnsDefaultValue()
        {
            // Arrange
            var objectType = new ObjectType("test");
            objectType.DefaultProperties["SpritePath"] = "default.png";
            var gameObject = new GameObject(objectType);

            // Act
            var spritePath = gameObject.GetProperty<string>("SpritePath");

            // Assert
            Assert.That(spritePath, Is.EqualTo("default.png"));
        }

        [Test]
        public void GetProperty_WhenPropertyDoesNotExist_ReturnsNull()
        {
            // Arrange
            var objectType = new ObjectType("test");
            var gameObject = new GameObject(objectType);

            // Act
            var spritePath = gameObject.GetProperty<string>("SpritePath");

            // Assert
            Assert.That(spritePath, Is.Null);
        }

        [Test]
        public void ObjectTypeManager_CanRegisterAndGetObjectType()
        {
            // Arrange
            var manager = new ObjectTypeManager();
            var objectType = new ObjectType("test");

            // Act
            manager.RegisterObjectType(objectType);
            var retrievedObjectType = manager.GetObjectType("test");

            // Assert
            Assert.That(retrievedObjectType, Is.SameAs(objectType));
        }

        [Test]
        public void RegisterObjectType_WhenCircularDependency_ThrowsException()
        {
            // Arrange
            var manager = new ObjectTypeManager();
            var typeA = new ObjectType("A") { ParentName = "C" };
            var typeB = new ObjectType("B") { ParentName = "A" };
            var typeC = new ObjectType("C") { ParentName = "B" };

            // Act
            manager.RegisterObjectType(typeA);
            manager.RegisterObjectType(typeB);

            // Assert
            Assert.Throws<System.InvalidOperationException>(() => manager.RegisterObjectType(typeC));
        }
    }
}
