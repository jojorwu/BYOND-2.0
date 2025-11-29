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
        private MapLoader _mapLoader = null!;
        private Project _project = null!;
        private string _projectPath = null!;

        [SetUp]
        public void SetUp()
        {
            _projectPath = Path.Combine(Path.GetTempPath(), Path.GetRandomFileName());
            Directory.CreateDirectory(_projectPath);
            _project = new Project(_projectPath);

            _gameState = new GameState();
            _objectTypeManager = new ObjectTypeManager();
            _mapLoader = new MapLoader(_objectTypeManager);
            _gameApi = new GameApi(_project, _gameState, _objectTypeManager, _mapLoader);
            _gameApi.SetMap(new Map());
            _gameApi.SetTurf(0, 0, 0, 1);

            var testObjectType = new ObjectType("test");
            _objectTypeManager.RegisterObjectType(testObjectType);
        }

        [TearDown]
        public void TearDown()
        {
            if (Directory.Exists(_projectPath))
            {
                Directory.Delete(_projectPath, true);
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

        [Test]
        public void LoadMap_WhenPathIsInvalid_ThrowsSecurityException()
        {
            // Arrange
            var invalidPath = "../../../../../../../etc/passwd";

            // Act & Assert
            Assert.Throws<System.Security.SecurityException>(() => _gameApi.LoadMap(invalidPath));
        }

        [Test]
        public void SaveMap_WhenPathIsInvalid_ThrowsSecurityException()
        {
            // Arrange
            var invalidPath = "../../../../../../../etc/passwd";

            // Act & Assert
            Assert.Throws<System.Security.SecurityException>(() => _gameApi.SaveMap(invalidPath));
        }
    }
}
