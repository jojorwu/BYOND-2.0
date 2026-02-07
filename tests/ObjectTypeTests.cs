using Shared;
using NUnit.Framework;
using Core;
using Core.Objects;
using Microsoft.Extensions.Logging.Abstractions;

namespace tests
{
    [TestFixture]
    public class ObjectTypeTests
    {
        [Test]
        public void GetVariable_WhenInstancePropertyExists_ReturnsInstanceValue()
        {
            // Arrange
            var objectType = new ObjectType(1, "test");
            objectType.VariableNames.Add("SpritePath");
            objectType.FlattenedDefaultValues.Add("default.png");
            var gameObject = new GameObject(objectType);
            gameObject.SetVariable("SpritePath", new DreamValue("instance.png"));

            // Act
            var spritePath = gameObject.GetVariable("SpritePath").ToString();

            // Assert
            Assert.That(spritePath, Is.EqualTo("instance.png"));
        }

        [Test]
        public void GetVariable_WhenInstancePropertyDoesNotExist_ReturnsDefaultValue()
        {
            // Arrange
            var objectType = new ObjectType(1, "test");
            objectType.VariableNames.Add("SpritePath");
            objectType.FlattenedDefaultValues.Add("default.png");
            var gameObject = new GameObject(objectType);

            // Act
            var spritePath = gameObject.GetVariable("SpritePath").ToString();

            // Assert
            Assert.That(spritePath, Is.EqualTo("default.png"));
        }

        [Test]
        public void GetVariable_WhenPropertyDoesNotExist_ReturnsNull()
        {
            // Arrange
            var objectType = new ObjectType(1, "test");
            var gameObject = new GameObject(objectType);

            // Act
            var spritePath = gameObject.GetVariable("SpritePath");

            // Assert
            Assert.That(spritePath, Is.EqualTo(DreamValue.Null));
        }

        [Test]
        public void ObjectTypeManager_CanRegisterAndGetObjectType()
        {
            // Arrange
            var manager = new ObjectTypeManager(NullLogger<ObjectTypeManager>.Instance);
            var objectType = new ObjectType(1, "test");

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
            var manager = new ObjectTypeManager(NullLogger<ObjectTypeManager>.Instance);
            var typeA = new ObjectType(1, "A") { ParentName = "C" };
            var typeB = new ObjectType(2, "B") { ParentName = "A" };
            var typeC = new ObjectType(3, "C") { ParentName = "B" };

            // Act
            manager.RegisterObjectType(typeA);
            manager.RegisterObjectType(typeB);

            // Assert
            Assert.Throws<System.InvalidOperationException>(() => manager.RegisterObjectType(typeC));
        }

        [Test]
        public void RegisterObjectType_WhenLongLinearHierarchy_DoesNotThrowException()
        {
            // Arrange
            var manager = new ObjectTypeManager(NullLogger<ObjectTypeManager>.Instance);
            var typeA = new ObjectType(1, "A");
            var typeB = new ObjectType(2, "B") { ParentName = "A" };
            var typeC = new ObjectType(3, "C") { ParentName = "B" };
            var typeD = new ObjectType(4, "D") { ParentName = "C" };
            var typeE = new ObjectType(5, "E") { ParentName = "D" };

            // Act & Assert
            Assert.DoesNotThrow(() =>
            {
                manager.RegisterObjectType(typeA);
                manager.RegisterObjectType(typeB);
                manager.RegisterObjectType(typeC);
                manager.RegisterObjectType(typeD);
                manager.RegisterObjectType(typeE);
            });
        }
    }
}
