using NUnit.Framework;
using Moq;
using Server;
using Server.Systems;
using Shared;
using Shared.Interfaces;
using Core.Regions;
using System.Threading;
using System.Threading.Tasks;
using System.Collections.Generic;
using Microsoft.Extensions.Options;

namespace tests
{
    [TestFixture]
    public class GameLoopStrategyTests
    {
        private Mock<IScriptHost> _scriptHostMock = null!;
        private Mock<IGameState> _gameStateMock = null!;
        private Mock<IUdpServer> _udpServerMock = null!;
        private Mock<IRegionManager> _regionManagerMock = null!;
        private Mock<IRegionActivationStrategy> _regionActivationStrategyMock = null!;
        private Mock<IGameStateSnapshotter> _gameStateSnapshotterMock = null!;
        private Mock<IJobSystem> _jobSystemMock = null!;
        private ServerSettings _serverSettings = null!;
        private CancellationTokenSource _cancellationTokenSource = null!;

        [SetUp]
        public void SetUp()
        {
            _scriptHostMock = new Mock<IScriptHost>();
            _gameStateMock = new Mock<IGameState>();
            _udpServerMock = new Mock<IUdpServer>();
            _regionManagerMock = new Mock<IRegionManager>();
            _regionActivationStrategyMock = new Mock<IRegionActivationStrategy>();
            _gameStateSnapshotterMock = new Mock<IGameStateSnapshotter>();
            _jobSystemMock = new Mock<IJobSystem>();
            _serverSettings = new ServerSettings();
            _cancellationTokenSource = new CancellationTokenSource();
        }

        [TearDown]
        public void TearDown()
        {
            _cancellationTokenSource.Dispose();
        }

        [Test]
        public void NetworkingSystem_Tick_CallsDependencies()
        {
            // Arrange
            var settings = new ServerSettings();
            var system = new Server.Systems.NetworkingSystem(_udpServerMock.Object, _gameStateSnapshotterMock.Object, _gameStateMock.Object, Options.Create(settings));
            _gameStateSnapshotterMock.Setup(gs => gs.GetSnapshot(_gameStateMock.Object)).Returns("snapshot");

            // Act
            system.Tick(new Mock<IEntityCommandBuffer>().Object);

            // Assert
            _gameStateSnapshotterMock.Verify(gs => gs.GetSnapshot(_gameStateMock.Object), Times.Once);
            // Broadcast is Task.Run, so we might need a small delay or use a different verify if we want to be sure,
            // but for unit test of system logic it's mostly about calling the snapshotter.
        }

        [Test]
        public async Task RegionalProcessingSystem_TickAsync_CallsDependencies()
        {
            // Arrange
            _serverSettings.Network.EnableBinarySnapshots = false; // Disable for this test to match existing expectations
            var strategy = new RegionalProcessingSystem(_scriptHostMock.Object, _regionManagerMock.Object, _regionActivationStrategyMock.Object, _udpServerMock.Object, _gameStateMock.Object, _gameStateSnapshotterMock.Object, _jobSystemMock.Object, Options.Create(_serverSettings));
            var activeRegions = new HashSet<Region> { new Region(new Robust.Shared.Maths.Vector2i(0,0), 0) };

            _jobSystemMock.Setup(js => js.ForEachAsync(It.IsAny<IEnumerable<List<(MergedRegion Region, List<IGameObject> Objects)>>>(), It.IsAny<System.Func<List<(MergedRegion Region, List<IGameObject> Objects)>, Task>>(), It.IsAny<JobPriority>()))
                .Returns(Task.CompletedTask)
                .Callback<IEnumerable<List<(MergedRegion Region, List<IGameObject> Objects)>>, System.Func<List<(MergedRegion Region, List<IGameObject> Objects)>, Task>, JobPriority>((items, func, priority) =>
                {
                    foreach (var item in items) func(item).Wait();
                });

            _regionActivationStrategyMock.Setup(rm => rm.GetActiveRegions()).Returns(activeRegions);
            _scriptHostMock.Setup(s => s.GetThreads()).Returns(new List<IScriptThread>());
            _scriptHostMock.Setup(s => s.ExecuteThreadsAsync(It.IsAny<IEnumerable<IScriptThread>>(), It.IsAny<IEnumerable<IGameObject>>(), It.IsAny<bool>(), It.IsAny<HashSet<int>>()))
                .ReturnsAsync(new List<IScriptThread>());
            _gameStateSnapshotterMock.Setup(gs => gs.GetSnapshot(_gameStateMock.Object, It.IsAny<MergedRegion>())).Returns("snapshot");

            // Act
            await strategy.TickAsync(_cancellationTokenSource.Token);

            // Assert
            _regionActivationStrategyMock.Verify(rm => rm.GetActiveRegions(), Times.Once);
            _gameStateSnapshotterMock.Verify(gs => gs.GetSnapshot(_gameStateMock.Object, It.IsAny<MergedRegion>()), Times.AtLeastOnce);
            _scriptHostMock.Verify(s => s.ExecuteThreadsAsync(It.IsAny<IEnumerable<IScriptThread>>(), It.IsAny<IEnumerable<IGameObject>>(), It.IsAny<bool>(), It.IsAny<HashSet<int>>()), Times.AtLeastOnce());
            _udpServerMock.Verify(u => u.BroadcastSnapshot(It.IsAny<MergedRegion>(), "snapshot"), Times.AtLeastOnce);
        }

        [Test]
        public void MergeRegions_MergesAdjacentRegions()
        {
            // Arrange
            _serverSettings.Performance.RegionalProcessing.EnableRegionMerging = true;
            _serverSettings.Performance.RegionalProcessing.MinRegionsToMerge = 2;
            var strategy = new RegionalProcessingSystem(_scriptHostMock.Object, _regionManagerMock.Object, _regionActivationStrategyMock.Object, _udpServerMock.Object, _gameStateMock.Object, _gameStateSnapshotterMock.Object, _jobSystemMock.Object, Options.Create(_serverSettings));

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
