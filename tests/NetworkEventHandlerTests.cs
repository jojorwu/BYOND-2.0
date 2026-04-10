using NUnit.Framework;
using Moq;
using Server;
using Shared;
using Shared.Interfaces;
using LiteNetLib;
using Microsoft.Extensions.Logging;

namespace tests
{
    [TestFixture]
    public class NetworkEventHandlerTests
    {
        private Mock<INetworkService> _networkServiceMock = null!;
        private Mock<IPlayerManager> _playerManagerMock = null!;
        private Mock<IInterestManager> _interestManagerMock = null!;
        private Mock<IScriptHost> _scriptHostMock = null!;
        private Mock<IServerContext> _serverContextMock = null!;
        private ServerSettings _serverSettings = null!;
        private NetworkEventHandler _networkEventHandler = null!;
        private Mock<IUdpServer> _udpServerMock = null!;
        private Mock<Shared.Config.IConsoleCommandManager> _commandManagerMock = null!;

        [SetUp]
        public void SetUp()
        {
            _networkServiceMock = new Mock<INetworkService>();
            _playerManagerMock = new Mock<IPlayerManager>();
            _interestManagerMock = new Mock<IInterestManager>();
            _scriptHostMock = new Mock<IScriptHost>();
            _serverSettings = new ServerSettings();
            _serverContextMock = new Mock<IServerContext>();
            _serverContextMock.Setup(c => c.PlayerManager).Returns(_playerManagerMock.Object);
            _serverContextMock.Setup(c => c.InterestManager).Returns(_interestManagerMock.Object);
            _serverContextMock.Setup(c => c.Settings).Returns(_serverSettings);
            _udpServerMock = new Mock<IUdpServer>();
            _commandManagerMock = new Mock<Shared.Config.IConsoleCommandManager>();

            _networkEventHandler = new NetworkEventHandler(_networkServiceMock.Object, _serverContextMock.Object, _scriptHostMock.Object, _udpServerMock.Object, _commandManagerMock.Object, new Mock<ILogger<NetworkEventHandler>>().Object);
            _networkEventHandler.SubscribeToEvents();
        }

        [Test]
        public void PeerConnected_AddsPlayerAndSendsServerInfo()
        {
            // Arrange
            var peerMock = new Mock<INetworkPeer>();

            // Act
            _networkServiceMock.Raise(ns => ns.PeerConnected += null, peerMock.Object);

            // Assert
            _playerManagerMock.Verify(pm => pm.AddPlayer(peerMock.Object), Times.Once);
            peerMock.Verify(p => p.SendAsync(It.IsAny<string>()), Times.Once);
        }

        [Test]
        public void PeerDisconnected_RemovesPlayer()
        {
            // Arrange
            var peerMock = new Mock<INetworkPeer>();
            var disconnectInfo = new DisconnectInfo();

            // Act
            _networkServiceMock.Raise(ns => ns.PeerDisconnected += null, peerMock.Object, disconnectInfo);

            // Assert
            _playerManagerMock.Verify(pm => pm.RemovePlayer(peerMock.Object), Times.Once);
        }

    }
}
