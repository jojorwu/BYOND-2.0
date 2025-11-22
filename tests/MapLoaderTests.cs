using NUnit.Framework;
using Core;

namespace Core.Tests
{
    [TestFixture]
    public class MapLoaderTests
    {
        [Test]
        public void LoadMap_WithMismatchedDimensions_DoesNotThrow()
        {
            // Arrange
            var mapPath = "maps/mismatched_map.json";

            // Act & Assert
            Assert.DoesNotThrow(() => MapLoader.LoadMap(mapPath));
        }
    }
}
