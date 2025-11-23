using NUnit.Framework;
using Core;

namespace Core.Tests
{
    [TestFixture]
    public class GameApiTests
    {
        private GameApi _gameApi = null!;
        private GameState _gameState = null!;
        private ObjectTypeManager _objectTypeManager = null!;
        private const string TestProjectDir = "TestProject";
        private Project _project = null!;
        private MapLoader _mapLoader = null!;

        [SetUp]
        public void SetUp()
        {
            Directory.CreateDirectory(TestProjectDir);
            _project = new Project(TestProjectDir);
            _gameState = new GameState();
            _objectTypeManager = new ObjectTypeManager(_project);
            _mapLoader = new MapLoader(_objectTypeManager, _project);
            _gameApi = new GameApi(_gameState, _objectTypeManager, _mapLoader, _project);
            _gameApi.CreateMap(1, 1, 1);
            _gameApi.SetTurf(0, 0, 0, 1);

            var testObjectType = new ObjectType("test");
            _objectTypeManager.RegisterObjectType(testObjectType);
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
        public void CreateObject_AddsObjectToTurfContents()
        {
            // Arrange
            var turf = _gameApi.GetTurf(0, 0, 0);

            // Act
            var obj = _gameApi.CreateObject("test", 0, 0, 0);

            // Assert
            Assert.That(turf, Is.Not.Null);
            Assert.That(turf?.Contents, Contains.Item(obj));
        }

        [Test]
        public void DestroyObject_RemovesObjectFromGameStateAndTurf()
        {
            // Arrange
            var obj = _gameApi.CreateObject("test", 0, 0, 0);
            Assert.That(obj, Is.Not.Null, "CreateObject returned null");

            var turf = _gameApi.GetTurf(0, 0, 0);
            Assert.That(turf, Is.Not.Null);
            Assert.That(turf?.Contents, Contains.Item(obj));

            // Act
            _gameApi.DestroyObject(obj.Id);

            // Assert
            Assert.That(_gameState.GameObjects.ContainsKey(obj.Id), Is.False);
            Assert.That(turf?.Contents, Does.Not.Contain(obj));
        }
    }
}
