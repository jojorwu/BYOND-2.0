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
        private const string TestProjectDir = "TestProject";
        private Project _project = null!;

        [SetUp]
        public void SetUp()
        {
            Directory.CreateDirectory(TestProjectDir);
            _project = new Project(TestProjectDir);
            _objectTypeManager = new ObjectTypeManager(_project);
            _mapLoader = new MapLoader(_objectTypeManager, _project);
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
    }
}
