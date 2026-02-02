using NUnit.Framework;
using Moq;
using Shared;
using Shared.Models;
using Shared.Services;
using Shared.Interfaces;
using Shared.Enums;
using Shared.Operations;
using Server;
using Core;
using Core.Players;
using LiteNetLib;
using System.Collections.Generic;

namespace tests
{
    [TestFixture]
    public class PlayerManagerTests
    {
        private Mock<IObjectApi> _objectApiMock = null!;
        private Mock<IObjectTypeManager> _objectTypeManagerMock = null!;
        private Mock<INetworkPeer> _networkPeerMock = null!;
        private PlayerManager _playerManager = null!;

        [SetUp]
        public void SetUp()
        {
            _objectApiMock = new Mock<IObjectApi>();
            _objectTypeManagerMock = new Mock<IObjectTypeManager>();
            _networkPeerMock = new Mock<INetworkPeer>();
            var serverSettings = new ServerSettings();
            _playerManager = new PlayerManager(_objectApiMock.Object, _objectTypeManagerMock.Object, serverSettings);
        }

        [Test]
        public void AddPlayer_CreatesPlayerObject()
        {
            // Arrange
            _objectTypeManagerMock.Setup(m => m.GetObjectType(It.IsAny<string>())).Returns(new ObjectType(1, "player"));

            // Act
            _playerManager.AddPlayer(_networkPeerMock.Object);

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
            _playerManager.AddPlayer(_networkPeerMock.Object);

            // Act
            _playerManager.RemovePlayer(_networkPeerMock.Object);

            // Assert
            _objectApiMock.Verify(o => o.DestroyObject(playerObject.Id), Times.Once);
        }
    }
}
