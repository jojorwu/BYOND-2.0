using Shared;
using NUnit.Framework;
using Moq;
using Server;
using Core;
using Microsoft.Extensions.Logging;
using System.Threading;
using System.Threading.Tasks;

namespace tests
{
    [TestFixture]
    public class GameLoopTests
    {
        private Mock<IGameState> _gameStateMock = null!;
        private Mock<IScriptHost> _scriptHostMock = null!;
        private Mock<IUdpServer> _udpServerMock = null!;
        private Mock<IRestartService> _restartServiceMock = null!;
        private ServerSettings _serverSettings = null!;
        private GameLoop _gameLoop = null!;
        private CancellationTokenSource _cancellationTokenSource = null!;

        [SetUp]
        public void SetUp()
        {
            _gameStateMock = new Mock<IGameState>();
            _scriptHostMock = new Mock<IScriptHost>();
            _udpServerMock = new Mock<IUdpServer>();
            _restartServiceMock = new Mock<IRestartService>();
            _serverSettings = new ServerSettings { Performance = { TickRate = 60 } };

            _gameLoop = new GameLoop(_scriptHostMock.Object, _udpServerMock.Object, _gameStateMock.Object, _serverSettings, _restartServiceMock.Object);
            _cancellationTokenSource = new CancellationTokenSource();
        }

        [TearDown]
        public void TearDown()
        {
            _cancellationTokenSource.Cancel();
            _gameLoop.Dispose();
            _cancellationTokenSource.Dispose();
        }

        [Test]
        public async Task StartAsync_CallsTickOnScriptHost()
        {
            // Arrange
            _cancellationTokenSource.CancelAfter(100);

            // Act
            await _gameLoop.StartAsync(_cancellationTokenSource.Token);
            await Task.Delay(50, _cancellationTokenSource.Token); // Give it a moment to tick

            // Assert
            _scriptHostMock.Verify(s => s.Tick(), Times.AtLeastOnce);
        }

        [Test]
        public async Task StartAsync_BroadcastsSnapshots()
        {
            // Arrange
            _cancellationTokenSource.CancelAfter(200);
            _gameStateMock.Setup(gs => gs.GetSnapshot()).Returns("snapshot");

            // Act
            await _gameLoop.StartAsync(_cancellationTokenSource.Token);
            await Task.Delay(100, _cancellationTokenSource.Token); // Give it a moment to tick and broadcast

            // Assert
            _udpServerMock.Verify(s => s.BroadcastSnapshot("snapshot"), Times.AtLeastOnce);
        }
    }
}
