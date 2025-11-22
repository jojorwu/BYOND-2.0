using NUnit.Framework;
using Core;

namespace Core.Tests
{
    [TestFixture]
    public class GameApiTests
    {
        private GameApi gameApi;
        private GameState gameState;

        [SetUp]
        public void SetUp()
        {
            gameState = new GameState();
            gameApi = new GameApi(gameState);
            gameApi.CreateMap(1, 1, 1);
            gameApi.SetTurf(0, 0, 0, 1);
        }

        [Test]
        public void CreateObject_AddsObjectToTurfContents()
        {
            // Arrange
            var turf = gameApi.GetTurf(0, 0, 0);

            // Act
            var obj = gameApi.CreateObject("test", 0, 0, 0);

            // Assert
            Assert.That(turf, Is.Not.Null);
            Assert.That(turf?.Contents, Contains.Item(obj));
        }

        [Test]
        public void DestroyObject_RemovesObjectFromGameStateAndTurf()
        {
            // Arrange
            var obj = gameApi.CreateObject("test", 0, 0, 0);
            var turf = gameApi.GetTurf(0, 0, 0);
            Assert.That(turf, Is.Not.Null);
            Assert.That(turf?.Contents, Contains.Item(obj));

            // Act
            gameApi.DestroyObject(obj.Id);

            // Assert
            Assert.That(gameState.GameObjects.ContainsKey(obj.Id), Is.False);
            Assert.That(turf?.Contents, Does.Not.Contain(obj));
        }
    }
}
