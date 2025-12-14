using NUnit.Framework;
using Moq;
using Shared;
using Core;
using System.Collections.Generic;
using Robust.Shared.Maths;

namespace tests
{
    [TestFixture]
    public class RegionManagerTests
    {
        private Mock<IMap> _mapMock = null!;
        private Mock<IScriptHost> _scriptHostMock = null!;
        private Mock<IGameState> _gameStateMock = null!;
        private RegionManager _regionManager = null!;

        [SetUp]
        public void SetUp()
        {
            _mapMock = new Mock<IMap>();
            _scriptHostMock = new Mock<IScriptHost>();
            _gameStateMock = new Mock<IGameState>();
            _regionManager = new RegionManager(_mapMock.Object, _scriptHostMock.Object, _gameStateMock.Object);
        }

        [Test]
        public void Initialize_CreatesRegionsAndAssignsChunks()
        {
            // Arrange
            var chunks = new List<(Vector2i, Chunk)>
            {
                (new Vector2i(0, 0), new Chunk()),
                (new Vector2i(1, 1), new Chunk()),
                (new Vector2i(Region.RegionSize, Region.RegionSize), new Chunk())
            };
            _mapMock.Setup(m => m.GetZLevels()).Returns(new List<int> { 0 });
            _mapMock.Setup(m => m.GetChunks(0)).Returns(chunks);

            // Act
            _regionManager.Initialize();

            // Assert
            var regions = _regionManager.GetRegions(0).ToList();
            Assert.That(regions.Count, Is.EqualTo(2));
        }

        [Test]
        public void Tick_TicksAllRegions()
        {
            // Arrange
            var chunks = new List<(Vector2i, Chunk)>
            {
                (new Vector2i(0, 0), new Chunk()),
            };
            _mapMock.Setup(m => m.GetZLevels()).Returns(new List<int> { 0 });
            _mapMock.Setup(m => m.GetChunks(0)).Returns(chunks);
            _regionManager.Initialize();

            // Act
            _regionManager.Tick();

            // Assert
            _scriptHostMock.Verify(s => s.Tick(It.IsAny<IEnumerable<IGameObject>>(), It.IsAny<bool>()), Times.Once);
        }
    }
}
