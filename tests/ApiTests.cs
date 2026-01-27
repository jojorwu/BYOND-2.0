using Shared;
using NUnit.Framework;
using Core;
using Core.Objects;
using Core.Maps;
using Core.Api;

namespace Core.Tests
{
    [TestFixture]
    public class ApiTests
    {
        private IMapApi _mapApi = null!;
        private IObjectApi _objectApi = null!;
        private IScriptApi _scriptApi = null!;
        private IStandardLibraryApi _standardLibraryApi = null!;
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
            _mapApi = new MapApi(_gameState, _mapLoader, _project, _objectTypeManager);
            _objectApi = new ObjectApi(_gameState, _objectTypeManager, _mapApi);
            _scriptApi = new ScriptApi(_project);
            var spatialQueryApi = new SpatialQueryApi(_gameState, _objectTypeManager, _mapApi);
            _standardLibraryApi = new StandardLibraryApi(spatialQueryApi);
            _mapApi.SetMap(new Map());
            var turfType = new ObjectType(1, "/turf");
            _objectTypeManager.RegisterObjectType(turfType);
            _mapApi.SetTurf(0, 0, 0, 1);

            var testObjectType = new ObjectType(2, "test");
            _objectTypeManager.RegisterObjectType(testObjectType);
        }

        [TearDown]
        public void TearDown()
        {
            _gameState.Dispose();
            if (Directory.Exists(_projectPath))
            {
                Directory.Delete(_projectPath, true);
            }
        }

        [Test]
        public void CreateObject_AddsObjectToTurfContents()
        {
            // Arrange
            var turf = _mapApi.GetTurf(0, 0, 0);

            // Act
            var obj = _objectApi.CreateObject(2, 0, 0, 0);

            // Assert
            Assert.That(turf, Is.Not.Null);
            Assert.That(turf?.Contents, Contains.Item(obj));
        }

        [Test]
        public void DestroyObject_RemovesObjectFromGameStateAndTurf()
        {
            // Arrange
            var obj = _objectApi.CreateObject(2, 0, 0, 0);
            Assert.That(obj, Is.Not.Null, "CreateObject returned null");

            var turf = _mapApi.GetTurf(0, 0, 0);
            Assert.That(turf, Is.Not.Null);
            Assert.That(turf?.Contents, Contains.Item(obj));

            // Act
            _objectApi.DestroyObject(obj.Id);

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
            Assert.ThrowsAsync<System.Security.SecurityException>(async () => await _mapApi.LoadMapAsync(invalidPath));
        }

        [Test]
        public void SaveMap_WhenPathIsInvalid_ThrowsSecurityException()
        {
            // Arrange
            var invalidPath = "../../../../../../../etc/passwd";

            // Act & Assert
            Assert.ThrowsAsync<System.Security.SecurityException>(async () => await _mapApi.SaveMapAsync(invalidPath));
        }
    }
}
