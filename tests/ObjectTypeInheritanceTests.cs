using NUnit.Framework;
using Core;

namespace Core.Tests
{
    [TestFixture]
    public class ObjectTypeInheritanceTests
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
        public void GetProperty_WhenPropertyIsInParent_ReturnsParentValue()
        {
            // Arrange
            var parent = new ObjectType("obj");
            parent.DefaultProperties["health"] = 100;
            _objectTypeManager.RegisterObjectType(parent);

            var child = new ObjectType("obj/item");
            _objectTypeManager.RegisterObjectType(child);

            var gameObject = new GameObject(child);

            // Act
            var health = gameObject.GetProperty<int>("health");

            // Assert
            Assert.That(health, Is.EqualTo(100));
        }

        [Test]
        public void GetProperty_WhenPropertyIsInChild_OverridesParentValue()
        {
            // Arrange
            var parent = new ObjectType("obj");
            parent.DefaultProperties["health"] = 100;
            _objectTypeManager.RegisterObjectType(parent);

            var child = new ObjectType("obj/item");
            child.DefaultProperties["health"] = 50;
            _objectTypeManager.RegisterObjectType(child);

            var gameObject = new GameObject(child);

            // Act
            var health = gameObject.GetProperty<int>("health");

            // Assert
            Assert.That(health, Is.EqualTo(50));
        }

        [Test]
        public void GetProperty_WhenPropertyIsOnInstance_OverridesAll()
        {
            // Arrange
            var parent = new ObjectType("obj");
            parent.DefaultProperties["health"] = 100;
            _objectTypeManager.RegisterObjectType(parent);

            var child = new ObjectType("obj/item");
            child.DefaultProperties["health"] = 50;
            _objectTypeManager.RegisterObjectType(child);

            var gameObject = new GameObject(child);
            gameObject.Properties["health"] = 25;

            // Act
            var health = gameObject.GetProperty<int>("health");

            // Assert
            Assert.That(health, Is.EqualTo(25));
        }
    }
}
