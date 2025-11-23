using NUnit.Framework;
using Core;

namespace tests
{
    [TestFixture]
    public class ObjectTypeTests
    {
        private ObjectTypeManager _objectTypeManager = null!;
        private const string TestProjectDir = "TestProject";
        private Project _project = null!;

        [SetUp]
        public void SetUp()
        {
            Directory.CreateDirectory(TestProjectDir);
            _project = new Project(TestProjectDir);
            _objectTypeManager = new ObjectTypeManager(_project);
        }

        [TearDown]
        public void TearDown()
        {
            if (Directory.Exists(TestProjectDir))
            {
                Directory.Delete(TestProjectDir, true);
            }
        }

        [Test]
        public void GetProperty_WhenInstancePropertyExists_ReturnsInstanceValue()
        {
            // Arrange
            var objectType = new ObjectType("test");
            objectType.DefaultProperties["SpritePath"] = "default.png";
            _objectTypeManager.RegisterObjectType(objectType);
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
            _objectTypeManager.RegisterObjectType(objectType);
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
            _objectTypeManager.RegisterObjectType(objectType);
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
            var manager = new ObjectTypeManager(_project);
            var objectType = new ObjectType("test");

            // Act
            manager.RegisterObjectType(objectType);
            var retrievedObjectType = manager.GetObjectType("test");

            // Assert
            Assert.That(retrievedObjectType, Is.SameAs(objectType));
        }
    }
}
