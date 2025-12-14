using NUnit.Framework;
using Moq;
using Shared;
using Core;
using Core.Api;
using Core.Objects;
using System.Collections.Generic;
using System.Linq;
using Robust.Shared.Maths;
using System;

namespace tests
{
    [TestFixture]
    public class MapApiTests
    {
        private Mock<IGameState> _gameStateMock = null!;
        private Mock<IMapLoader> _mapLoaderMock = null!;
        private Mock<IProject> _projectMock = null!;
        private Mock<IObjectTypeManager> _objectTypeManagerMock = null!;
        private SpatialGrid _spatialGrid = null!; // Use a real instance instead of a mock
        private MapApi _mapApi = null!;

        [SetUp]
        public void SetUp()
        {
            _gameStateMock = new Mock<IGameState>();
            _mapLoaderMock = new Mock<IMapLoader>();
            _projectMock = new Mock<IProject>();
            _objectTypeManagerMock = new Mock<IObjectTypeManager>();
            _spatialGrid = new SpatialGrid(); // Initialize the real instance

            _gameStateMock.Setup(gs => gs.SpatialGrid).Returns(_spatialGrid); // Return the real instance
            _gameStateMock.Setup(gs => gs.ReadLock()).Returns(new Mock<IDisposable>().Object);

            _mapApi = new MapApi(_gameStateMock.Object, _mapLoaderMock.Object, _projectMock.Object, _objectTypeManagerMock.Object);
        }

        [Test]
        public void GetObjectsInRange_ReturnsCorrectObjects()
        {
            // Arrange
            var objType = new ObjectType(1, "/obj");
            var monsterType = new ObjectType(2, "/obj/monster") { Parent = objType };
            var itemType = new ObjectType(3, "/obj/item") { Parent = objType };

            var obj1 = new GameObject(monsterType, 1, 1, 0); // In range
            var obj2 = new GameObject(itemType, 2, 2, 0);    // In range
            var obj3 = new GameObject(monsterType, 5, 5, 0); // Out of range
            var obj4 = new GameObject(monsterType, 1, 1, 1); // Wrong Z-level

            // Add objects directly to the real grid
            _spatialGrid.Add(obj1);
            _spatialGrid.Add(obj2);
            _spatialGrid.Add(obj3);
            _spatialGrid.Add(obj4);

            _objectTypeManagerMock.Setup(m => m.GetObjectType("/obj")).Returns(objType);

            // Act
            var result = _mapApi.GetObjectsInRange(0, 0, 0, 3).ToList();

            // Assert
            Assert.That(result.Count, Is.EqualTo(2));
            Assert.That(result.Contains(obj1), Is.True);
            Assert.That(result.Contains(obj2), Is.True);
        }

        [Test]
        public void GetObjectsInRange_WithTypeFilter_ReturnsCorrectObjects()
        {
            // Arrange
            var objType = new ObjectType(1, "/obj");
            var monsterType = new ObjectType(2, "/obj/monster") { Parent = objType };
            var itemType = new ObjectType(3, "/obj/item") { Parent = objType };

            var obj1 = new GameObject(monsterType, 1, 1, 0); // In range, correct type
            var obj2 = new GameObject(itemType, 2, 2, 0);    // In range, wrong type
            var obj3 = new GameObject(monsterType, 5, 5, 0); // Out of range

            _spatialGrid.Add(obj1);
            _spatialGrid.Add(obj2);
            _spatialGrid.Add(obj3);

            _objectTypeManagerMock.Setup(m => m.GetObjectType("/obj/monster")).Returns(monsterType);

            // Act
            var result = _mapApi.GetObjectsInRange(0, 0, 0, 3, "/obj/monster").ToList();

            // Assert
            Assert.That(result.Count, Is.EqualTo(1));
            Assert.That(result[0], Is.EqualTo(obj1));
        }

        [Test]
        public void GetObjectsInArea_ReturnsCorrectObjects()
        {
            // Arrange
            var objType = new ObjectType(1, "/obj");
            var obj1 = new GameObject(objType, 0, 0, 0);
            var obj2 = new GameObject(objType, 10, 10, 0);
            var obj3 = new GameObject(objType, 11, 11, 0);

            _spatialGrid.Add(obj1);
            _spatialGrid.Add(obj2);
            _spatialGrid.Add(obj3);

            _objectTypeManagerMock.Setup(m => m.GetObjectType("/obj")).Returns(objType);

            // Act
            var result = _mapApi.GetObjectsInArea(0, 0, 10, 10, 0).ToList();

            // Assert
            Assert.That(result.Count, Is.EqualTo(2));
            Assert.That(result.Contains(obj1), Is.True);
            Assert.That(result.Contains(obj2), Is.True);
        }
    }
}
