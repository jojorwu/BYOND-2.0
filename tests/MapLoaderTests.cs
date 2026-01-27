using Shared;
using NUnit.Framework;
using Core;
using System.IO;
using Core.Objects;
using Core.Maps;
using System.Threading.Tasks;

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
        public async Task SaveAndLoadMap_PreservesGameObjectsAndProperties()
        {
            // Arrange
            var objectType = new ObjectType(1, "test_object");
            objectType.VariableNames.Add("SpritePath");
            objectType.FlattenedDefaultValues.Add("default.png");
            objectType.VariableNames.Add("InstanceProp");
            objectType.FlattenedDefaultValues.Add(null);

            _objectTypeManager.RegisterObjectType(objectType);

            var map = new Map();
            var turf = new Turf(1);
            var gameObject = new GameObject(objectType);
            gameObject.SetVariable("InstanceProp", new DreamValue("instance_value"));
            turf.Contents.Add(gameObject);
            map.SetTurf(17, 33, 0, turf); // Coordinates that will fall into a non-zero chunk

            // Act
            await _mapLoader.SaveMapAsync(map, TestMapPath);
            var loadedMap = await _mapLoader.LoadMapAsync(TestMapPath);

            // Assert
            Assert.That(loadedMap, Is.Not.Null);
            var loadedTurf = loadedMap.GetTurf(17, 33, 0);
            Assert.That(loadedTurf, Is.Not.Null);
            Assert.That(loadedTurf.Contents, Has.Count.EqualTo(1));

            var loadedGameObject = loadedTurf.Contents[0];
            Assert.That(loadedGameObject.ObjectType.Name, Is.EqualTo("test_object"));
            Assert.That(loadedGameObject.GetVariable("SpritePath").ToString(), Is.EqualTo("default.png"));
            Assert.That(loadedGameObject.GetVariable("InstanceProp").ToString(), Is.EqualTo("instance_value"));
        }

        [Test]
        public void GetAndSetTurf_WithNegativeCoordinates_WorksCorrectly()
        {
            // Arrange
            var map = new Map();
            var turf = new Turf(1);

            // Act
            map.SetTurf(-1, -1, 0, turf);
            var retrievedTurf = map.GetTurf(-1, -1, 0);

            // Assert
            Assert.That(retrievedTurf, Is.Not.Null);
            Assert.That(retrievedTurf, Is.EqualTo(turf));
        }
    }
}
