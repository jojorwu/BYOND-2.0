using NUnit.Framework;
using Moq;
using Server;
using Shared;
using System.Threading;
using System.Threading.Tasks;
using System.Collections.Generic;

namespace tests
{
    [TestFixture]
    public class GameLoopStrategyTests
    {
        private Mock<IScriptHost> _scriptHostMock = null!;
        private Mock<IGameState> _gameStateMock = null!;
        private Mock<IUdpServer> _udpServerMock = null!;
        private Mock<IRegionManager> _regionManagerMock = null!;
        private Mock<IGameStateSnapshotter> _gameStateSnapshotterMock = null!;
        private ServerSettings _serverSettings = null!;
        private CancellationTokenSource _cancellationTokenSource = null!;

        [SetUp]
        public void SetUp()
        {
            _scriptHostMock = new Mock<IScriptHost>();
            _gameStateMock = new Mock<IGameState>();
            _udpServerMock = new Mock<IUdpServer>();
            _regionManagerMock = new Mock<IRegionManager>();
            _gameStateSnapshotterMock = new Mock<IGameStateSnapshotter>();
            _serverSettings = new ServerSettings();
            _cancellationTokenSource = new CancellationTokenSource();
        }

        [TearDown]
        public void TearDown()
        {
            _cancellationTokenSource.Dispose();
        }

        [Test]
        public async Task GlobalGameLoopStrategy_TickAsync_CallsDependencies()
        {
            // Arrange
            var strategy = new GlobalGameLoopStrategy(_scriptHostMock.Object, _gameStateMock.Object, _gameStateSnapshotterMock.Object, _udpServerMock.Object);
            _gameStateSnapshotterMock.Setup(gs => gs.GetSnapshot(_gameStateMock.Object)).Returns("snapshot");

            // Act
            await strategy.TickAsync(_cancellationTokenSource.Token);

            // Assert
            _scriptHostMock.Verify(s => s.Tick(), Times.Once);
            _gameStateSnapshotterMock.Verify(gs => gs.GetSnapshot(_gameStateMock.Object), Times.Once);
            _udpServerMock.Verify(u => u.BroadcastSnapshot("snapshot"), Times.Once);
        }

        [Test]
        public async Task RegionalGameLoopStrategy_TickAsync_CallsDependencies()
        {
            // Arrange
            var strategy = new RegionalGameLoopStrategy(_scriptHostMock.Object, _regionManagerMock.Object, _udpServerMock.Object, _gameStateMock.Object, _gameStateSnapshotterMock.Object, _serverSettings);
            var activeRegions = new HashSet<Region> { new Region(new Robust.Shared.Maths.Vector2i(0,0), 0) };

            _regionManagerMock.Setup(rm => rm.GetActiveRegions()).Returns(activeRegions);
            _scriptHostMock.Setup(s => s.GetThreads()).Returns(new List<IScriptThread>());
            _gameStateSnapshotterMock.Setup(gs => gs.GetSnapshot(_gameStateMock.Object, It.IsAny<MergedRegion>())).Returns("snapshot");

            // Act
            await strategy.TickAsync(_cancellationTokenSource.Token);

            // Assert
            _regionManagerMock.Verify(rm => rm.GetActiveRegions(), Times.Once);
            _gameStateSnapshotterMock.Verify(gs => gs.GetSnapshot(_gameStateMock.Object, It.IsAny<MergedRegion>()), Times.AtLeastOnce);
            _scriptHostMock.Verify(s => s.ExecuteThreads(It.IsAny<List<IScriptThread>>(), It.IsAny<IEnumerable<IGameObject>>(), It.IsAny<bool>()), Times.AtLeastOnce());
            _udpServerMock.Verify(u => u.BroadcastSnapshot(It.IsAny<MergedRegion>(), "snapshot"), Times.AtLeastOnce);
        }

        [Test]
        public void MergeRegions_MergesAdjacentRegions()
        {
            // Arrange
            _serverSettings.Performance.RegionalProcessing.EnableRegionMerging = true;
            _serverSettings.Performance.RegionalProcessing.MinRegionsToMerge = 2;
            var strategy = new RegionalGameLoopStrategy(_scriptHostMock.Object, _regionManagerMock.Object, _udpServerMock.Object, _gameStateMock.Object, _gameStateSnapshotterMock.Object, _serverSettings);

            var region1 = new Region(new Robust.Shared.Maths.Vector2i(0, 0), 0);
            var region2 = new Region(new Robust.Shared.Maths.Vector2i(1, 0), 0);
            var region3 = new Region(new Robust.Shared.Maths.Vector2i(3, 3), 0); // Non-adjacent
            var activeRegions = new HashSet<Region> { region1, region2, region3 };

            _regionManagerMock.Setup(rm => rm.TryGetRegion(0, new Robust.Shared.Maths.Vector2i(1, 0), out It.Ref<Region?>.IsAny))
                .Returns(new TryGetRegionDelegate((int z, Robust.Shared.Maths.Vector2i coords, out Region? region) => { region = region2; return true; }));
            _regionManagerMock.Setup(rm => rm.TryGetRegion(0, new Robust.Shared.Maths.Vector2i(-1, 0), out It.Ref<Region?>.IsAny))
                .Returns(new TryGetRegionDelegate((int z, Robust.Shared.Maths.Vector2i coords, out Region? region) => { region = null; return false; }));
             _regionManagerMock.Setup(rm => rm.TryGetRegion(0, new Robust.Shared.Maths.Vector2i(0, 1), out It.Ref<Region?>.IsAny))
                .Returns(new TryGetRegionDelegate((int z, Robust.Shared.Maths.Vector2i coords, out Region? region) => { region = null; return false; }));
            _regionManagerMock.Setup(rm => rm.TryGetRegion(0, new Robust.Shared.Maths.Vector2i(0, -1), out It.Ref<Region?>.IsAny))
                .Returns(new TryGetRegionDelegate((int z, Robust.Shared.Maths.Vector2i coords, out Region? region) => { region = null; return false; }));

            // Act
            var mergedRegions = strategy.MergeRegions(activeRegions);

            // Assert
            Assert.That(mergedRegions.Count, Is.EqualTo(2)); // region1 and region2 should merge, region3 remains separate

            var mergedGroup = mergedRegions.First(mr => mr.Regions.Count > 1);
            var mergedChunkCount = mergedGroup.Regions.SelectMany(r => r.GetChunks()).Count();
            var originalChunkCount = region1.GetChunks().Count() + region2.GetChunks().Count();
            Assert.That(mergedChunkCount, Is.EqualTo(originalChunkCount));
        }

        private delegate bool TryGetRegionDelegate(int z, Robust.Shared.Maths.Vector2i coords, out Region? region);
    }
}
