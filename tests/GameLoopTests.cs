using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Moq;
using NUnit.Framework;
using Server;
using Shared;

namespace tests
{
    [TestFixture]
    public class GameLoopTests
    {
        private Mock<IScriptHost> _scriptHostMock = null!;
        private Mock<IUdpServer> _udpServerMock = null!;
        private Mock<IGameState> _gameStateMock = null!;
        private ServerSettings _serverSettings = null!;
        private Mock<ILogger<GameLoop>> _loggerMock = null!;
        private GameLoop _gameLoop = null!;

        [SetUp]
        public void Setup()
        {
            _scriptHostMock = new Mock<IScriptHost>();
            _udpServerMock = new Mock<IUdpServer>();
            _gameStateMock = new Mock<IGameState>();
            _serverSettings = new ServerSettings(); // Use default settings
            _loggerMock = new Mock<ILogger<GameLoop>>();

            _gameLoop = new GameLoop(
                _scriptHostMock.Object,
                _udpServerMock.Object,
                _gameStateMock.Object,
                _serverSettings,
                _loggerMock.Object
            );
        }

        [TearDown]
        public void TearDown()
        {
            _gameLoop.Dispose();
        }

        [Test]
        public async Task StartAsync_StartsGameLoopAndSnapshotBroadcast()
        {
            // Act
            await _gameLoop.StartAsync(CancellationToken.None);
            await Task.Delay(100); // Allow time for the loop to run a few times
            await _gameLoop.StopAsync(CancellationToken.None);

            // Assert
            _scriptHostMock.Verify(s => s.Tick(), Times.AtLeastOnce);
            _gameStateMock.Verify(g => g.GetSnapshot(), Times.AtLeastOnce);
            _udpServerMock.Verify(u => u.BroadcastSnapshot(It.IsAny<string>()), Times.AtLeastOnce);
        }

        [Test]
        public async Task StopAsync_StopsGameLoopAndSnapshotBroadcast()
        {
            // Arrange
            var cts = new CancellationTokenSource();
            await _gameLoop.StartAsync(cts.Token);
            await Task.Delay(50);

            // Act
            await _gameLoop.StopAsync(CancellationToken.None);
            await Task.Delay(100); // Give time for tasks to stop

            // Assert
            _scriptHostMock.Invocations.Clear();
            _gameStateMock.Invocations.Clear();
            await Task.Delay(100);

            _scriptHostMock.Verify(s => s.Tick(), Times.Never);
            _gameStateMock.Verify(g => g.GetSnapshot(), Times.Never);
        }
    }
}
