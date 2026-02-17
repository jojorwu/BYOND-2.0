using NUnit.Framework;
using Moq;
using Shared;
using Core;
using Core.Regions;
using System.Collections.Generic;
using System.Linq;
using Robust.Shared.Maths;
using System;
using Shared.Services;
using Microsoft.Extensions.Options;

namespace tests
{
    [TestFixture]
    public class PlayerBasedActivationStrategyTests
    {
        private Mock<IPlayerManager> _playerManagerMock = null!;
        private Mock<IRegionManager> _regionManagerMock = null!;
        private ServerSettings _serverSettings = null!;
        private PlayerBasedActivationStrategy _strategy = null!;

        [SetUp]
        public void SetUp()
        {
            _playerManagerMock = new Mock<IPlayerManager>();
            _regionManagerMock = new Mock<IRegionManager>();
            _serverSettings = new ServerSettings();
            _strategy = new PlayerBasedActivationStrategy(_playerManagerMock.Object, _regionManagerMock.Object, Options.Create(_serverSettings));
        }

        [Test]
        public void GetActiveRegions_ReturnsCorrectRegions()
        {
            // Arrange
            _serverSettings.Performance.RegionalProcessing.ActivationRange = 0;
            var region = new Region(new Vector2i(0, 0), 0);
            _regionManagerMock.Setup(r => r.TryGetRegion(0, new Vector2i(0,0), out region)).Returns(true);
            _playerManagerMock.Setup(p => p.ForEachPlayerObject(It.IsAny<Action<IGameObject>>()))
                .Callback<Action<IGameObject>>(action => action(new GameObject(new ObjectType(1, "player"), 0, 0, 0)));

            // Act
            var activeRegions = _strategy.GetActiveRegions();

            // Assert
            Assert.That(activeRegions.Count, Is.EqualTo(1));
            Assert.That(activeRegions.Contains(region), Is.True);
        }

        [Test]
        public void GetActiveRegions_WithCustomActivationRange_ReturnsCorrectRegions()
        {
            // Arrange
            _serverSettings.Performance.RegionalProcessing.ActivationRange = 1;
            _serverSettings.Performance.RegionalProcessing.ZActivationRange = 0;

            // Create a 3x3 grid of regions
            var regions = new Dictionary<Vector2i, Region>();
            for (int x = -1; x <= 1; x++)
            {
                for (int y = -1; y <= 1; y++)
                {
                    var coords = new Vector2i(x, y);
                    var region = new Region(coords, 0);
                    regions[coords] = region;
                    _regionManagerMock.Setup(r => r.TryGetRegion(0, coords, out region)).Returns(true);
                }
            }

            _playerManagerMock.Setup(p => p.ForEachPlayerObject(It.IsAny<Action<IGameObject>>()))
                .Callback<Action<IGameObject>>(action => action(new GameObject(new ObjectType(1, "player"), 0, 0, 0)));

            // Act
            var activeRegions = _strategy.GetActiveRegions();

            // Assert
            Assert.That(activeRegions.Count, Is.EqualTo(9)); // 3x3 grid
        }
    }
}
