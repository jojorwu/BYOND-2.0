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

        [SetUp]
        public void SetUp()
        {
            _gameState = new GameState();
            _objectTypeManager = new ObjectTypeManager();
            _gameApi = new GameApi(_gameState, _objectTypeManager);
            _gameApi.CreateMap(1, 1, 1);
            _gameApi.SetTurf(0, 0, 0, 1);

            var testObjectType = new ObjectType("test");
            _objectTypeManager.RegisterObjectType(testObjectType);
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
