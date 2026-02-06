using NUnit.Framework;
using Moq;
using Shared;
using Core.Regions;
using System.Collections.Generic;
using Microsoft.Extensions.Options;
using System.Linq;
using Robust.Shared.Maths;

namespace tests
{
    [TestFixture]
    public class RegionManagerTests
    {
        private Mock<IMap> _mapMock = null!;
        private ServerSettings _serverSettings = null!;
        private RegionManager _regionManager = null!;

        [SetUp]
        public void SetUp()
        {
            _mapMock = new Mock<IMap>();
            _serverSettings = new ServerSettings();
            _regionManager = new RegionManager(_mapMock.Object, Options.Create(_serverSettings));
        }

        [Test]
        public void Initialize_CreatesRegionsAndAssignsChunks()
        {
            // Arrange
            _serverSettings.Performance.RegionalProcessing.RegionSize = 4;
            var regionSize = _serverSettings.Performance.RegionalProcessing.RegionSize;
            var chunks = new List<(Vector2i, Chunk)>
            {
                (new Vector2i(0, 0), new Chunk()),
                (new Vector2i(1, 1), new Chunk()),
                (new Vector2i(regionSize, regionSize), new Chunk())
            };
            _mapMock.Setup(m => m.GetZLevels()).Returns(new List<int> { 0 });
            _mapMock.Setup(m => m.GetChunks(0)).Returns(chunks);

            // Act
            _regionManager.Initialize();

            // Assert
            var regions = _regionManager.GetRegions(0).ToList();
            Assert.That(regions.Count, Is.EqualTo(2));
            Assert.That(regions.First(r => r.Coords == new Vector2i(0,0)).GetChunks().Count(), Is.EqualTo(2));
            Assert.That(regions.First(r => r.Coords == new Vector2i(1,1)).GetChunks().Count(), Is.EqualTo(1));
        }
    }
}
