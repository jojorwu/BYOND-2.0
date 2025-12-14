using NUnit.Framework;
using Moq;
using Shared;
using Core;
using Core.Regions;
using System.Collections.Generic;
using System.Linq;
using Robust.Shared.Maths;
using System;
using Core.Objects;

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
            _serverSettings = new ServerSettings(); // Ensure fresh settings for each test
            _regionManager = new RegionManager(_mapMock.Object, _scriptHostMock.Object, _gameStateMock.Object, _playerManagerMock.Object, _serverSettings);
        }

        [Test]
        public void Initialize_CreatesRegionsAndAssignsChunks()
        {
            // Arrange
            _serverSettings.Performance.RegionalProcessing.RegionSize = 4; // Use a smaller, custom size for test
            var regionSize = _serverSettings.Performance.RegionalProcessing.RegionSize;
            var chunks = new List<(Vector2i, Chunk)>
            {
                (new Vector2i(0, 0), new Chunk()),
                (new Vector2i(1, 1), new Chunk()),
                (new Vector2i(regionSize, regionSize), new Chunk())
            };
            _mapMock.Setup(m => m.GetZLevels()).Returns(new List<int> { 0 });
            _mapMock.Setup(m => m.GetChunks(0)).Returns(chunks);

            // Act
            _regionManager.Initialize();

            // Assert
            var regions = _regionManager.GetRegions(0).ToList();
            Assert.That(regions.Count, Is.EqualTo(2));
            Assert.That(regions.First(r => r.Coords == new Vector2i(0,0)).GetChunks().Count(), Is.EqualTo(2));
            Assert.That(regions.First(r => r.Coords == new Vector2i(1,1)).GetChunks().Count(), Is.EqualTo(1));
        }

        [Test]
        public void GetActiveRegions_ReturnsCorrectRegions()
        {
            // Arrange
            _serverSettings.Performance.RegionalProcessing.ActivationRange = 0; // Only the player's region
            var regionSize = _serverSettings.Performance.RegionalProcessing.RegionSize;
            var chunks = new List<(Vector2i, Chunk)>
            {
                (new Vector2i(0, 0), new Chunk()), // Player is here
                (new Vector2i(regionSize * 2, regionSize * 2), new Chunk()) // An inactive region far away
            };
            _mapMock.Setup(m => m.GetZLevels()).Returns(new List<int> { 0 });
            _mapMock.Setup(m => m.GetChunks(0)).Returns(chunks);
            _playerManagerMock.Setup(p => p.ForEachPlayerObject(It.IsAny<Action<IGameObject>>()))
                .Callback<Action<IGameObject>>(action => action(new GameObject(new ObjectType(1, "player"), 0, 0, 0)));
            _regionManager.Initialize();

            // Act
            var activeRegions = _regionManager.GetActiveRegions();

            // Assert
            Assert.That(activeRegions.Count, Is.EqualTo(1)); // With range 0, only the player's region is active
        }

        [Test]
        public void GetActiveRegions_WithCustomActivationRange_ReturnsCorrectRegions()
        {
            // Arrange
            _serverSettings.Performance.RegionalProcessing.ActivationRange = 2;
            _serverSettings.Performance.RegionalProcessing.ZActivationRange = 1;
            var regionSize = _serverSettings.Performance.RegionalProcessing.RegionSize;

            var zLevels = new List<int> { 0, 1, 2 }; // z=2 should not be activated
            _mapMock.Setup(m => m.GetZLevels()).Returns(zLevels);

            // Create a 5x5 grid of regions on three z-levels
            for (int z = 0; z < 3; z++)
            {
                 var chunks = new List<(Vector2i, Chunk)>();
                for (int x = -2; x <= 2; x++)
                {
                    for (int y = -2; y <= 2; y++)
                    {
                        chunks.Add((new Vector2i(x * regionSize, y * regionSize), new Chunk()));
                    }
                }
                _mapMock.Setup(m => m.GetChunks(z)).Returns(chunks);
            }

            // Place a player in the central region (0,0) on z-level 0
            _playerManagerMock.Setup(p => p.ForEachPlayerObject(It.IsAny<Action<IGameObject>>()))
                .Callback<Action<IGameObject>>(action => action(new GameObject(new ObjectType(1, "player"), 0, 0, 0)));
            _regionManager.Initialize();

            // Act
            var activeRegions = _regionManager.GetActiveRegions();

            // Assert
            // ActivationRange = 2 -> 5x5 grid = 25 regions
            // ZActivationRange = 1 -> z-levels 0 and 1 are active
            // Total = 25 (on z=0) + 25 (on z=1) = 50
            Assert.That(activeRegions.Count, Is.EqualTo(50));
        }

        [Test]
        public void MergeRegions_MergesAdjacentRegions()
        {
            // Arrange
            _serverSettings.Performance.RegionalProcessing.EnableRegionMerging = true;
            var regionSize = _serverSettings.Performance.RegionalProcessing.RegionSize;
            var chunks = new List<(Vector2i, Chunk)>
            {
                (new Vector2i(0, 0), new Chunk()),
                (new Vector2i(regionSize, 0), new Chunk())
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
