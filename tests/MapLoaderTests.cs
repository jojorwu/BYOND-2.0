using NUnit.Framework;
using Core;
using System.IO;

namespace tests
{
    [TestFixture]
    public class MapLoaderTests
    {
        private const string TestMapPath = "test_map.json";
        private ObjectTypeManager _objectTypeManager = null!;
        private MapLoader _mapLoader = null!;

        [SetUp]
        public void SetUp()
        {
            _objectTypeManager = new ObjectTypeManager();
            _mapLoader = new MapLoader(_objectTypeManager);
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
        public void SaveAndLoadMap_PreservesGameObjectsAndProperties()
        {
            // Arrange
            var objectType = new ObjectType("test_object");
            objectType.DefaultProperties["SpritePath"] = "default.png";
            _objectTypeManager.RegisterObjectType(objectType);

            var map = new Map(1, 1, 1);
            var turf = new Turf(1);
            var gameObject = new GameObject(objectType);
            gameObject.Properties["InstanceProp"] = "instance_value";
            turf.Contents.Add(gameObject);
            map.SetTurf(0, 0, 0, turf);

            // Act
            _mapLoader.SaveMap(map, TestMapPath);
            var loadedMap = _mapLoader.LoadMap(TestMapPath);

            // Assert
            Assert.That(loadedMap, Is.Not.Null);
            var loadedTurf = loadedMap.GetTurf(0, 0, 0);
            Assert.That(loadedTurf, Is.Not.Null);
            Assert.That(loadedTurf.Contents, Has.Count.EqualTo(1));

            var loadedGameObject = loadedTurf.Contents[0];
            Assert.That(loadedGameObject.ObjectType.Name, Is.EqualTo("test_object"));
            Assert.That(loadedGameObject.GetProperty<string>("SpritePath"), Is.EqualTo("default.png"));
            Assert.That(loadedGameObject.GetProperty<string>("InstanceProp"), Is.EqualTo("instance_value"));
        }

        [Test]
        public void SaveAndLoadMap_HandlesNonCubicDimensions()
        {
            // Arrange
            var map = new Map(10, 5, 2); // Non-cubic dimensions
            var turf = new Turf(1);
            map.SetTurf(9, 4, 1, turf);

            // Act & Assert
            Assert.DoesNotThrow(() =>
            {
                _mapLoader.SaveMap(map, TestMapPath);
            }, "Saving a non-cubic map should not throw an exception.");

            Map? loadedMap = null;
            Assert.DoesNotThrow(() =>
            {
                loadedMap = _mapLoader.LoadMap(TestMapPath);
            }, "Loading a non-cubic map should not throw an exception.");

            Assert.That(loadedMap, Is.Not.Null);
            Assert.That(loadedMap.Width, Is.EqualTo(10));
            Assert.That(loadedMap.Height, Is.EqualTo(5));
            Assert.That(loadedMap.Depth, Is.EqualTo(2));
            Assert.That(loadedMap.GetTurf(9, 4, 1), Is.Not.Null);
        }
    }
}
