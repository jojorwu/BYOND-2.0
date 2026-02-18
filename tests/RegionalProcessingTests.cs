using NUnit.Framework;
using Moq;
using Server;
using Shared;
using Shared.Interfaces;
using Shared.Services;
using Core.Regions;
using System.Threading;
using System.Threading.Tasks;
using System.Collections.Generic;
using Microsoft.Extensions.Options;
using System.Linq;

namespace tests
{
    [TestFixture]
    public class RegionalProcessingTests
    {
        private Mock<IScriptHost> _scriptHostMock = null!;
        private Mock<IRegionManager> _regionManagerMock = null!;
        private Mock<IRegionActivationStrategy> _regionActivationStrategyMock = null!;
        private Mock<IUdpServer> _udpServerMock = null!;
        private Mock<IGameState> _gameStateMock = null!;
        private Mock<IGameStateSnapshotter> _gameStateSnapshotterMock = null!;
        private Mock<IJobSystem> _jobSystemMock = null!;
        private ServerSettings _serverSettings = null!;

        [SetUp]
        public void SetUp()
        {
            _scriptHostMock = new Mock<IScriptHost>();
            _regionManagerMock = new Mock<IRegionManager>();
            _regionActivationStrategyMock = new Mock<IRegionActivationStrategy>();
            _udpServerMock = new Mock<IUdpServer>();
            _gameStateMock = new Mock<IGameState>();
            _gameStateSnapshotterMock = new Mock<IGameStateSnapshotter>();
            _jobSystemMock = new Mock<IJobSystem>();
            _serverSettings = new ServerSettings();

            // Default mock behavior for ForEachAsync (Action overload)
            _jobSystemMock.Setup(js => js.ForEachAsync(It.IsAny<IEnumerable<It.IsAnyType>>(), It.IsAny<Action<It.IsAnyType>>(), It.IsAny<JobPriority>()))
                .Returns((System.Collections.IEnumerable source, object action, JobPriority priority) =>
                {
                    foreach (var item in source)
                    {
                        var method = action.GetType().GetMethod("Invoke");
                        method!.Invoke(action, new[] { item });
                    }
                    return Task.CompletedTask;
                });

            // Default mock behavior for ForEachAsync (Func overload)
            _jobSystemMock.Setup(js => js.ForEachAsync(It.IsAny<IEnumerable<It.IsAnyType>>(), It.IsAny<System.Func<It.IsAnyType, Task>>(), It.IsAny<JobPriority>()))
                .Returns(async (System.Collections.IEnumerable source, object action, JobPriority priority) =>
                {
                    foreach (var item in source)
                    {
                        var method = action.GetType().GetMethod("Invoke");
                        var task = (Task)method!.Invoke(action, new[] { item })!;
                        await task;
                    }
                });
        }

        [Test]
        public async Task TickAsync_UsesJobSystemForParallelProcessing()
        {
            // Arrange
            _serverSettings.Network.EnableBinarySnapshots = true;
            var strategy = new RegionalGameLoopStrategy(
                _scriptHostMock.Object,
                _regionManagerMock.Object,
                _regionActivationStrategyMock.Object,
                _udpServerMock.Object,
                _gameStateMock.Object,
                _gameStateSnapshotterMock.Object,
                _jobSystemMock.Object,
                Options.Create(_serverSettings));

            var region = new Region(new Robust.Shared.Maths.Vector2i(0, 0), 0);
            _regionActivationStrategyMock.Setup(r => r.GetActiveRegions()).Returns(new HashSet<Region> { region });
            _scriptHostMock.Setup(s => s.GetThreads()).Returns(new List<IScriptThread>());
            _scriptHostMock.Setup(s => s.ExecuteThreadsAsync(It.IsAny<IEnumerable<IScriptThread>>(), It.IsAny<IEnumerable<IGameObject>>(), It.IsAny<bool>(), It.IsAny<HashSet<int>>()))
                .ReturnsAsync(new List<IScriptThread>());

            // Act
            await strategy.TickAsync(CancellationToken.None);

            // Assert
            _jobSystemMock.Verify(js => js.ForEachAsync(
                It.IsAny<IEnumerable<List<(MergedRegion Region, List<IGameObject> Objects)>>>(),
                It.IsAny<System.Func<List<(MergedRegion Region, List<IGameObject> Objects)>, Task>>(),
                It.IsAny<JobPriority>()), Times.Once);

            _udpServerMock.Verify(u => u.SendRegionSnapshotAsync(It.IsAny<MergedRegion>(), It.IsAny<IEnumerable<IGameObject>>()), Times.AtLeastOnce);
        }

        [Test]
        public async Task UdpServer_SendRegionSnapshotAsync_ParallelizesPerPlayer()
        {
            // Arrange
            var playerManagerMock = new Mock<IPlayerManager>();
            var contextMock = new Mock<IServerContext>();
            contextMock.Setup(c => c.PlayerManager).Returns(playerManagerMock.Object);

            var snapshotServiceMock = new Mock<BinarySnapshotService>(new Mock<StringInterner>().Object);
            var interestManagerMock = new Mock<IInterestManager>();

            var udpServer = new UdpServer(
                new Mock<INetworkService>().Object,
                new Mock<NetworkEventHandler>(new Mock<INetworkService>().Object, contextMock.Object, new Mock<IScriptHost>().Object).Object,
                contextMock.Object,
                snapshotServiceMock.Object,
                interestManagerMock.Object,
                _jobSystemMock.Object);

            var region = new Region(new Robust.Shared.Maths.Vector2i(0, 0), 0);
            var mergedRegion = new MergedRegion(new List<Region> { region });
            var peer1 = new Mock<INetworkPeer>().Object;
            var peer2 = new Mock<INetworkPeer>().Object;

            playerManagerMock.Setup(pm => pm.ForEachPlayerInRegion(region, It.IsAny<Action<INetworkPeer>>()))
                .Callback<Region, Action<INetworkPeer>>((r, action) => {
                    action(peer1);
                    action(peer2);
                });

            // Act
            await udpServer.SendRegionSnapshotAsync(mergedRegion, new List<IGameObject>());

            // Assert
            _jobSystemMock.Verify(js => js.ForEachAsync(
                It.Is<IEnumerable<INetworkPeer>>(p => p.Count() == 2),
                It.IsAny<Action<INetworkPeer>>(),
                It.IsAny<JobPriority>()), Times.Once);
        }
    }
}
