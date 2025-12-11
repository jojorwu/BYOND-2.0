using NUnit.Framework;
using Shared;
using System.IO;
using System.Linq;

namespace Core.Tests
{
    [TestFixture]
    public class Ss14MapLoaderTests
    {
        private const string TestMapPath = "test_map.yml";
        private Ss14MapLoader _ss14MapLoader = null!;
        private ObjectTypeManager _objectTypeManager = null!;

        [SetUp]
        public void SetUp()
        {
            _objectTypeManager = new ObjectTypeManager();
            // Register types that are expected to exist, simulating a real project environment.
            _objectTypeManager.RegisterObjectType(new ObjectType(1, "Floor"));
            _objectTypeManager.RegisterObjectType(new ObjectType(2, "Wall"));
            _objectTypeManager.RegisterObjectType(new ObjectType(3, "Window"));

            _ss14MapLoader = new Ss14MapLoader(_objectTypeManager);
        }

        [TearDown]
        public void TearDown()
        {
            if (File.Exists(TestMapPath))
            {
                File.Delete(TestMapPath);
            }
        }

        [Test]
        public void LoadMap_CorrectlySeparatesTurfsAndObjects()
        {
            // Arrange
            var yaml = @"
# This entity should become the turf itself.
- id: Floor
  components:
  - type: Transform
    pos: 5, 5

# This entity should be placed as an object on top of the floor.
- id: Window
  components:
  - type: Transform
    pos: 5, 5

# This is just another turf at a different location.
- id: Wall
  components:
  - type: Transform
    pos: 6, 5
";
            File.WriteAllText(TestMapPath, yaml);

            // Act
            var map = _ss14MapLoader.LoadMap(TestMapPath);

            // Assert
            Assert.That(map, Is.Not.Null);

            // Check the turf at (5, 5) which should contain the floor and the window
            var turfWithWindow = map.GetTurf(5, 5, 0);
            Assert.That(turfWithWindow, Is.Not.Null, "Turf at (5,5) should exist.");
            Assert.That(turfWithWindow.Contents, Has.Count.EqualTo(2), "Turf at (5,5) should have two objects: the floor and the window.");

            // The first object defines the turf type
            Assert.That(turfWithWindow.Contents[0].ObjectType.Name, Is.EqualTo("Floor"), "The turf's base type should be Floor.");

            // Check for the window object on the turf
            var window = turfWithWindow.Contents.FirstOrDefault(go => go.ObjectType.Name == "Window");
            Assert.That(window, Is.Not.Null, "Window object should be present on the turf at (5,5).");

            // Check the wall at (6, 5)
            var wallTurf = map.GetTurf(6, 5, 0);
            Assert.That(wallTurf, Is.Not.Null, "Turf at (6,5) should exist.");
            Assert.That(wallTurf.Contents, Has.Count.EqualTo(1), "Wall turf should have one object.");
            Assert.That(wallTurf.Contents[0].ObjectType.Name, Is.EqualTo("Wall"), "The turf's type at (6,5) should be Wall.");
        }
    }
}
