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
        public void LoadMap_LoadsEntitiesAndPositions()
        {
            // Arrange
            var yaml = @"
- id: Wall
  components:
  - type: Transform
    pos: 1, 2
- id: Floor
  components:
  - type: Transform
    pos: 3, 4";
            File.WriteAllText(TestMapPath, yaml);

            // Act
            var map = _ss14MapLoader.LoadMap(TestMapPath);

            // Assert
            Assert.That(map, Is.Not.Null);
            var wall = map.GetTurf(1, 2, 0)?.Contents[0];
            Assert.That(wall, Is.Not.Null);
            Assert.That(wall.ObjectType.Name, Is.EqualTo("Wall"));
            var floor = map.GetTurf(3, 4, 0)?.Contents[0];
            Assert.That(floor, Is.Not.Null);
            Assert.That(floor.ObjectType.Name, Is.EqualTo("Floor"));
        }

        [Test]
        public void LoadMap_HandlesMultipleEntitiesOnSameTurf()
        {
            // Arrange
            var yaml = @"
- id: Window
  components:
  - type: Transform
    pos: 5, 5
- id: Grille
  components:
  - type: Transform
    pos: 5, 5";
            File.WriteAllText(TestMapPath, yaml);

            // Act
            var map = _ss14MapLoader.LoadMap(TestMapPath);

            // Assert
            Assert.That(map, Is.Not.Null);
            var turf = map.GetTurf(5, 5, 0);
            Assert.That(turf, Is.Not.Null);
            Assert.That(turf.Contents, Has.Count.EqualTo(2));
            Assert.That(turf.Contents.Any(go => go.ObjectType.Name == "Window"), Is.True);
            Assert.That(turf.Contents.Any(go => go.ObjectType.Name == "Grille"), Is.True);
        }
    }
}
