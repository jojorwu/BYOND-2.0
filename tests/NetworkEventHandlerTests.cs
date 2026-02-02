using NUnit.Framework;
using Moq;
using Server;
using Shared;
using Shared.Models;
using Shared.Services;
using Shared.Interfaces;
using Shared.Enums;
using Shared.Operations;
using LiteNetLib;

namespace tests
{
    [TestFixture]
    public class NetworkEventHandlerTests
    {
        private Mock<INetworkService> _networkServiceMock = null!;
        private Mock<IPlayerManager> _playerManagerMock = null!;
        private Mock<IScriptHost> _scriptHostMock = null!;
        private Mock<IServerContext> _serverContextMock = null!;
        private ServerSettings _serverSettings = null!;
        private NetworkEventHandler _networkEventHandler = null!;

        [SetUp]
        public void SetUp()
        {
            _networkServiceMock = new Mock<INetworkService>();
            _playerManagerMock = new Mock<IPlayerManager>();
            _scriptHostMock = new Mock<IScriptHost>();
            _serverSettings = new ServerSettings();
            _serverContextMock = new Mock<IServerContext>();
            _serverContextMock.Setup(c => c.PlayerManager).Returns(_playerManagerMock.Object);
            _serverContextMock.Setup(c => c.Settings).Returns(_serverSettings);

            _networkEventHandler = new NetworkEventHandler(_networkServiceMock.Object, _serverContextMock.Object, _scriptHostMock.Object);
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
            peerMock.Verify(p => p.Send(It.IsAny<string>()), Times.Once);
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

        [Test]
        public void CommandReceived_EnqueuesCommand()
        {
            // Arrange
            var peerMock = new Mock<INetworkPeer>();
            var command = "test_command";

            // Act
            _networkServiceMock.Raise(ns => ns.CommandReceived += null, peerMock.Object, command);

            // Assert
            _scriptHostMock.Verify(sh => sh.EnqueueCommand(command, It.IsAny<Action<string>>()), Times.Once);
        }
    }
}
