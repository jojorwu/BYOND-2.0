using NUnit.Framework;
using Moq;
using Server;
using Server.Events;
using Shared;
using Shared.Interfaces;
using LiteNetLib;

namespace tests
{
    [TestFixture]
    public class NetworkEventHandlerTests
    {
        private Mock<Shared.Messaging.IEventBus> _eventBusMock = null!;
        private Mock<IPlayerManager> _playerManagerMock = null!;
        private Mock<IInterestManager> _interestManagerMock = null!;
        private Mock<IScriptHost> _scriptHostMock = null!;
        private Mock<IServerContext> _serverContextMock = null!;
        private ServerSettings _serverSettings = null!;
        private NetworkEventHandler _networkEventHandler = null!;

        [SetUp]
        public void SetUp()
        {
            _eventBusMock = new Mock<Shared.Messaging.IEventBus>();
            _playerManagerMock = new Mock<IPlayerManager>();
            _interestManagerMock = new Mock<IInterestManager>();
            _scriptHostMock = new Mock<IScriptHost>();
            _serverSettings = new ServerSettings();
            _serverContextMock = new Mock<IServerContext>();
            _serverContextMock.Setup(c => c.PlayerManager).Returns(_playerManagerMock.Object);
            _serverContextMock.Setup(c => c.InterestManager).Returns(_interestManagerMock.Object);
            _serverContextMock.Setup(c => c.Settings).Returns(_serverSettings);

            _networkEventHandler = new NetworkEventHandler(_eventBusMock.Object, _serverContextMock.Object, _scriptHostMock.Object);
        }

        [Test]
        public void PeerConnected_AddsPlayerAndSendsServerInfo()
        {
            // Arrange
            var peerMock = new Mock<INetworkPeer>();
            Action<PeerConnectedEvent>? callback = null;
            _eventBusMock.Setup(eb => eb.Subscribe(It.IsAny<Action<PeerConnectedEvent>>()))
                .Callback<Action<PeerConnectedEvent>>(a => callback = a);

            _networkEventHandler.SubscribeToEvents();

            // Act
            callback?.Invoke(new PeerConnectedEvent(peerMock.Object));

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
            Action<PeerDisconnectedEvent>? callback = null;
            _eventBusMock.Setup(eb => eb.Subscribe(It.IsAny<Action<PeerDisconnectedEvent>>()))
                .Callback<Action<PeerDisconnectedEvent>>(a => callback = a);

            _networkEventHandler.SubscribeToEvents();

            // Act
            callback?.Invoke(new PeerDisconnectedEvent(peerMock.Object, disconnectInfo));

            // Assert
            _playerManagerMock.Verify(pm => pm.RemovePlayer(peerMock.Object), Times.Once);
        }

        [Test]
        public void CommandReceived_EnqueuesCommand()
        {
            // Arrange
            var peerMock = new Mock<INetworkPeer>();
            var command = "test_command";
            Action<CommandReceivedEvent>? callback = null;
            _eventBusMock.Setup(eb => eb.Subscribe(It.IsAny<Action<CommandReceivedEvent>>()))
                .Callback<Action<CommandReceivedEvent>>(a => callback = a);

            _networkEventHandler.SubscribeToEvents();

            // Act
            callback?.Invoke(new CommandReceivedEvent(peerMock.Object, command));

            // Assert
            _scriptHostMock.Verify(sh => sh.EnqueueCommand(command, It.IsAny<Action<string>>()), Times.Once);
        }
    }
}
