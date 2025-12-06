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
    public class GameTests
    {
        private Mock<GameState> _gameStateMock = null!;
        private Mock<ScriptHost> _scriptHostMock = null!;
        private Mock<UdpServer> _udpServerMock = null!;
        private Mock<ILogger<Server.Game>> _loggerMock = null!;
        private ServerSettings _serverSettings = null!;
        private Server.Game _game = null!;
        private CancellationTokenSource _cancellationTokenSource = null!;

        [SetUp]
        public void SetUp()
        {
            _gameStateMock = new Mock<GameState>();

            var projectMock = new Mock<Project>(".");
            var settings = new ServerSettings();
            var serviceProviderMock = new Mock<System.IServiceProvider>();
            var scriptHostLoggerMock = new Mock<ILogger<ScriptHost>>();
            _scriptHostMock = new Mock<ScriptHost>(projectMock.Object, settings, serviceProviderMock.Object, scriptHostLoggerMock.Object);

            var udpLoggerMock = new Mock<ILogger<UdpServer>>();
            var scriptHostInterfaceMock = new Mock<IScriptHost>();
            _udpServerMock = new Mock<UdpServer>(scriptHostInterfaceMock.Object, _gameStateMock.Object, settings, udpLoggerMock.Object);

            _loggerMock = new Mock<ILogger<Server.Game>>();
            _serverSettings = new ServerSettings();

            _game = new Server.Game(_gameStateMock.Object, _scriptHostMock.Object, _udpServerMock.Object, _serverSettings, _loggerMock.Object);
            _cancellationTokenSource = new CancellationTokenSource();
        }

        [TearDown]
        public void TearDown()
        {
            _cancellationTokenSource.Cancel();
            _game.Dispose();
            _cancellationTokenSource.Dispose();
        }

        [Test]
        public async Task ExecuteAsync_CallsTickOnScriptHost()
        {
            // Arrange
            _cancellationTokenSource.CancelAfter(100);

            // Act
            await _game.StartAsync(_cancellationTokenSource.Token);

            // Assert
            _scriptHostMock.Verify(s => s.Tick(), Times.AtLeastOnce);
        }

        [Test]
        public async Task ExecuteAsync_BroadcastsSnapshots()
        {
            // Arrange
            _cancellationTokenSource.CancelAfter(200);
            _gameStateMock.Setup(gs => gs.GetSnapshot()).Returns("snapshot");

            // Act
            await _game.StartAsync(_cancellationTokenSource.Token);

            // Assert
            _udpServerMock.Verify(s => s.BroadcastSnapshot("snapshot"), Times.AtLeastOnce);
        }
    }
}
