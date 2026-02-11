using Shared;
using NUnit.Framework;
using Core.VM.Runtime;
using Core.VM.Procs;
using Core.Objects;
using Core.Api;
using Moq;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

namespace tests
{
    [TestFixture]
    public class MovementTests
    {
        private DreamVM _vm = null!;
        private GameState _gameState = null!;
        private ObjectTypeManager _typeManager = null!;

        [SetUp]
        public void SetUp()
        {
            _gameState = new GameState();
            _typeManager = new ObjectTypeManager(NullLogger<ObjectTypeManager>.Instance);

            var settings = Options.Create(new ServerSettings());
            _vm = new DreamVM(settings, NullLogger<DreamVM>.Instance, new INativeProcProvider[] { new StandardNativeProcProvider() });
            _vm.GameState = _gameState;
            _vm.ObjectTypeManager = _typeManager;

            var mapMock = new Mock<IMapApi>();
            var spatialQueryApi = new SpatialQueryApi(_gameState, _typeManager, mapMock.Object);
            var standardLibraryApi = new StandardLibraryApi(spatialQueryApi, mapMock.Object);

            var gameApiMock = new Mock<IGameApi>();
            gameApiMock.Setup(m => m.StdLib).Returns(standardLibraryApi);
            _vm.GameApi = gameApiMock.Object;

            // Setup a basic turf
            var turfType = new ObjectType(1, "/turf");
            _typeManager.RegisterObjectType(turfType);
            mapMock.Setup(m => m.GetTurf(It.IsAny<int>(), It.IsAny<int>(), It.IsAny<int>()))
                   .Returns((int x, int y, int z) => (ITurf)new Turf(turfType, x, y, z));
        }

        [TearDown]
        public void TearDown()
        {
            _vm.Dispose();
            _gameState.Dispose();
        }

        [Test]
        public void Step_MovesObject_Test()
        {
            var mobType = new ObjectType(2, "/mob");
            var mob = new GameObject(mobType, 1, 1, 0);

            var nativeProcs = new StandardNativeProcProvider().GetNativeProcs();
            var stepProc = (NativeProc)nativeProcs["step"];

            var thread = new DreamThread(new DreamProc("test", Array.Empty<byte>(), Array.Empty<string>(), 0), _vm.Context, 1000);

            // step(mob, NORTH)
            var result = stepProc.Call(thread, null, new[] { new DreamValue(mob), new DreamValue(1f) });

            Assert.That(result.AsFloat(), Is.EqualTo(1f));
            Assert.That(mob.X, Is.EqualTo(1));
            Assert.That(mob.Y, Is.EqualTo(2));
        }

        [Test]
        public void StepTo_MovesObjectTowardsTarget_Test()
        {
            var mobType = new ObjectType(2, "/mob");
            var mob = new GameObject(mobType, 1, 1, 0);
            var target = new GameObject(mobType, 3, 3, 0);

            var nativeProcs = new StandardNativeProcProvider().GetNativeProcs();
            var stepToProc = (NativeProc)nativeProcs["step_to"];

            var thread = new DreamThread(new DreamProc("test", Array.Empty<byte>(), Array.Empty<string>(), 0), _vm.Context, 1000);

            // step_to(mob, target)
            var result = stepToProc.Call(thread, null, new[] { new DreamValue(mob), new DreamValue(target) });

            Assert.That(result.AsFloat(), Is.EqualTo(1f));
            Assert.That(mob.X, Is.EqualTo(2));
            Assert.That(mob.Y, Is.EqualTo(2));
        }

        [Test]
        public void Step_BlockedByNullTurf_ReturnsZero_Test()
        {
            var mobType = new ObjectType(2, "/mob");
            var mob = new GameObject(mobType, 100, 100, 0);

            // Re-setup to return null for specific turf
            var mapMock = new Mock<IMapApi>();
            mapMock.Setup(m => m.GetTurf(It.IsAny<int>(), It.IsAny<int>(), It.IsAny<int>())).Returns((ITurf?)null);

            var spatialQueryApi = new SpatialQueryApi(_gameState, _typeManager, mapMock.Object);
            var standardLibraryApi = new StandardLibraryApi(spatialQueryApi, mapMock.Object);
            var gameApiMock = new Mock<IGameApi>();
            gameApiMock.Setup(m => m.StdLib).Returns(standardLibraryApi);
            _vm.GameApi = gameApiMock.Object;

            var nativeProcs = new StandardNativeProcProvider().GetNativeProcs();
            var stepProc = (NativeProc)nativeProcs["step"];
            var thread = new DreamThread(new DreamProc("test", Array.Empty<byte>(), Array.Empty<string>(), 0), _vm.Context, 1000);

            // step(mob, NORTH) -> should be blocked by null turf
            var result = stepProc.Call(thread, null, new[] { new DreamValue(mob), new DreamValue(1f) });

            Assert.That(result.AsFloat(), Is.EqualTo(0f));
            Assert.That(mob.X, Is.EqualTo(100));
            Assert.That(mob.Y, Is.EqualTo(100));
        }
    }
}
