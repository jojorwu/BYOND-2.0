using NUnit.Framework;
using Shared;
using Shared.Interfaces;
using Shared.Services;
using Microsoft.Extensions.Logging.Abstractions;
using System.Linq;
using Robust.Shared.Maths;

namespace tests
{
    [TestFixture]
    public class SpatialGridSyncTests
    {
        [Test]
        public void GameObject_Moving_UpdatesSpatialGrid()
        {
            var grid = new SpatialGrid(NullLogger<SpatialGrid>.Instance, TimeProvider.System, MockDiagnosticBus.Instance, 16);
            var state = new GameState(grid);
            var obj = new GameObject(new ObjectType(1, "/obj"), 5, 5, 0);

            state.AddGameObject(obj);

            // Initially in cell (0,0)
            var initialObjects = grid.GetObjectsInBox(new Box3l(0, 0, 0, 32, 32, 0));
            Assert.That(initialObjects.Contains(obj), Is.True);

            // Move to (20, 20) -> should be in cell (1,1)
            obj.X = 20;
            obj.Y = 20;

            var oldCellObjects = grid.GetObjectsInBox(new Box3l(0, 0, 0, 15, 15, 0));
            var newCellObjects = grid.GetObjectsInBox(new Box3l(16, 16, 0, 31, 31, 0));

            Assert.That(oldCellObjects.Contains(obj), Is.False);
            Assert.That(newCellObjects.Contains(obj), Is.True);
        }
    }
}
