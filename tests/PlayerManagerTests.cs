using NUnit.Framework;
using Moq;
using Shared;
using Server;
using LiteNetLib;
using System.Collections.Generic;

namespace tests
{
    [TestFixture]
    public class PlayerManagerTests
    {
        private Mock<IObjectApi> _objectApiMock = null!;
        private Mock<IObjectTypeManager> _objectTypeManagerMock = null!;
        private NetPeer _netPeer = null!;
        private PlayerManager _playerManager = null!;

        [SetUp]
        public void SetUp()
        {
            _objectApiMock = new Mock<IObjectApi>();
            _objectTypeManagerMock = new Mock<IObjectTypeManager>();
            var mockListener = new Mock<INetEventListener>();
            var netManager = new NetManager(mockListener.Object);
            // Can't mock NetPeer, so we create a real one. This is ugly, but it works.
            netManager.Start(0);
            netManager.Connect("localhost", 0, "key");
            _netPeer = netManager.FirstPeer;
            while(_netPeer == null)
            {
                netManager.PollEvents();
                _netPeer = netManager.FirstPeer;
            }

            var serverSettings = new ServerSettings();
            _playerManager = new PlayerManager(_objectApiMock.Object, _objectTypeManagerMock.Object, serverSettings);
        }

        [Test]
        public void AddPlayer_CreatesPlayerObject()
        {
            // Arrange
            _objectTypeManagerMock.Setup(m => m.GetObjectType(It.IsAny<string>())).Returns(new ObjectType(1, "player"));

            // Act
            _playerManager.AddPlayer(_netPeer);

            // Assert
            _objectApiMock.Verify(o => o.CreateObject(1, 0, 0, 0), Times.Once);
        }

        [Test]
        public void RemovePlayer_DestroysPlayerObject()
        {
            // Arrange
            var playerObject = new GameObject(new ObjectType(1, "player"), 0, 0, 0);
            _objectTypeManagerMock.Setup(m => m.GetObjectType(It.IsAny<string>())).Returns(new ObjectType(1, "player"));
            _objectApiMock.Setup(o => o.CreateObject(1, 0, 0, 0)).Returns(playerObject);
            _playerManager.AddPlayer(_netPeer);

            // Act
            _playerManager.RemovePlayer(_netPeer);

            // Assert
            _objectApiMock.Verify(o => o.DestroyObject(playerObject.Id), Times.Once);
        }
    }
}
