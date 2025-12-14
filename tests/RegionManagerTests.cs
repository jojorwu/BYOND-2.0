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
        private Mock<IPlayerManager> _playerManagerMock = null!;
        private ServerSettings _serverSettings = null!;
        private RegionManager _regionManager = null!;

        [SetUp]
        public void SetUp()
        {
            _mapMock = new Mock<IMap>();
            _scriptHostMock = new Mock<IScriptHost>();
            _gameStateMock = new Mock<IGameState>();
            _playerManagerMock = new Mock<IPlayerManager>();
            _serverSettings = new ServerSettings();
            _regionManager = new RegionManager(_mapMock.Object, _scriptHostMock.Object, _gameStateMock.Object, _playerManagerMock.Object, _serverSettings);
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
        public void GetActiveRegions_ReturnsCorrectRegions()
        {
            // Arrange
            var chunks = new List<(Vector2i, Chunk)>
            {
                (new Vector2i(0, 0), new Chunk()),
                (new Vector2i(Region.RegionSize * 2, Region.RegionSize * 2), new Chunk())
            };
            _mapMock.Setup(m => m.GetZLevels()).Returns(new List<int> { 0 });
            _mapMock.Setup(m => m.GetChunks(0)).Returns(chunks);
            _playerManagerMock.Setup(p => p.ForEachPlayerObject(It.IsAny<Action<IGameObject>>()))
                .Callback<Action<IGameObject>>(action => action(new GameObject(new ObjectType(1, "player"), 0, 0, 0)));
            _regionManager.Initialize();

            // Act
            var activeRegions = _regionManager.GetActiveRegions();

            // Assert
            Assert.That(activeRegions.Count, Is.EqualTo(1));
        }

        [Test]
        public void MergeRegions_MergesAdjacentRegions()
        {
            // Arrange
            _serverSettings.Performance.RegionalProcessing.EnableRegionMerging = true;
            var chunks = new List<(Vector2i, Chunk)>
            {
                (new Vector2i(0, 0), new Chunk()),
                (new Vector2i(Region.RegionSize, 0), new Chunk())
            };
            _mapMock.Setup(m => m.GetZLevels()).Returns(new List<int> { 0 });
            _mapMock.Setup(m => m.GetChunks(0)).Returns(chunks);
            _regionManager.Initialize();
            var activeRegions = _regionManager.GetRegions(0).ToHashSet();

            // Act
            var mergedRegions = _regionManager.MergeRegions(activeRegions);

            // Assert
            Assert.That(mergedRegions.Count, Is.EqualTo(1));
        }
    }
}
