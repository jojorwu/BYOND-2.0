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
            var chunks = new List<((long X, long Y), Chunk)>
            {
                ((0, 0), new Chunk()),
                ((1, 1), new Chunk()),
                ((regionSize, regionSize), new Chunk())
            };
            _mapMock.Setup(m => m.GetZLevels()).Returns(new List<int> { 0 });
            _mapMock.Setup(m => m.GetChunks(0)).Returns(chunks);

            // Act
            _regionManager.Initialize();

            // Assert
            var regions = _regionManager.GetRegions(0).ToList();
            Assert.That(regions.Count, Is.EqualTo(2));
            Assert.That(regions.First(r => r.Coords == (0L, 0L)).GetChunks().Count(), Is.EqualTo(2));
            Assert.That(regions.First(r => r.Coords == (1L, 1L)).GetChunks().Count(), Is.EqualTo(1));
        }
    }
}
