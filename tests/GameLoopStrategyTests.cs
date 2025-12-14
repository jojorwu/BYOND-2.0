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
        private CancellationTokenSource _cancellationTokenSource = null!;

        [SetUp]
        public void SetUp()
        {
            _scriptHostMock = new Mock<IScriptHost>();
            _gameStateMock = new Mock<IGameState>();
            _udpServerMock = new Mock<IUdpServer>();
            _regionManagerMock = new Mock<IRegionManager>();
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
            var strategy = new GlobalGameLoopStrategy(_scriptHostMock.Object, _gameStateMock.Object, _udpServerMock.Object);
            _gameStateMock.Setup(gs => gs.GetSnapshot()).Returns("snapshot");

            // Act
            await strategy.TickAsync(_cancellationTokenSource.Token);

            // Assert
            _scriptHostMock.Verify(s => s.Tick(), Times.Once);
            _gameStateMock.Verify(gs => gs.GetSnapshot(), Times.Once);
            _udpServerMock.Verify(u => u.BroadcastSnapshot("snapshot"), Times.Once);
        }

        [Test]
        public async Task RegionalGameLoopStrategy_TickAsync_CallsDependencies()
        {
            // Arrange
            var strategy = new RegionalGameLoopStrategy(_scriptHostMock.Object, _regionManagerMock.Object, _udpServerMock.Object);
            var regionData = new List<(MergedRegion, string, IEnumerable<IGameObject>)>
            {
                (new MergedRegion(new List<Region>()), "snapshot1", new List<IGameObject>()),
                (new MergedRegion(new List<Region>()), "snapshot2", new List<IGameObject>())
            };
            _regionManagerMock.Setup(rm => rm.Tick()).ReturnsAsync(regionData);
            _scriptHostMock.Setup(s => s.GetThreads()).Returns(new List<IScriptThread>());

            // Act
            await strategy.TickAsync(_cancellationTokenSource.Token);

            // Assert
            _regionManagerMock.Verify(rm => rm.Tick(), Times.Once);
            _scriptHostMock.Verify(s => s.ExecuteThreads(It.IsAny<List<IScriptThread>>(), It.IsAny<IEnumerable<IGameObject>>(), It.IsAny<bool>()), Times.AtLeastOnce());
            _udpServerMock.Verify(u => u.BroadcastSnapshot(It.IsAny<MergedRegion>(), It.IsAny<string>()), Times.Exactly(2));
        }
    }
}
