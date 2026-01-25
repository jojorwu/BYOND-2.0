using NUnit.Framework;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Shared;
using Core.Objects;
using Core.Maps;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;

namespace Core.Tests
{
    [TestFixture]
    public class DmmServiceTests
    {
        private GameState _gameState = null!;
        private ObjectTypeManager _objectTypeManager = null!;
        private Project _project = null!;
        private string _projectPath = null!;
        private DmmService _dmmService = null!;
        private Mock<IDmmParserService> _dmmParserServiceMock = null!;

        [SetUp]
        public void SetUp()
        {
            _projectPath = Path.Combine(Path.GetTempPath(), "dmm_loader_test_" + Path.GetRandomFileName());
            Directory.CreateDirectory(_projectPath);
            Directory.CreateDirectory(Path.Combine(_projectPath, "maps"));
            Directory.CreateDirectory(Path.Combine(_projectPath, "scripts"));

            _project = new Project(_projectPath);
            _gameState = new GameState();
            _objectTypeManager = new ObjectTypeManager();
            var dreamMakerLoader = new DreamMakerLoader(_objectTypeManager, _project);
            var logger = new NullLogger<DmmService>();
            _dmmParserServiceMock = new Mock<IDmmParserService>();
            _dmmService = new DmmService(_objectTypeManager, _project, dreamMakerLoader, logger, _dmmParserServiceMock.Object);
        }

        [TearDown]
        public void TearDown()
        {
            _gameState.Dispose();
            if (Directory.Exists(_projectPath))
            {
                Directory.Delete(_projectPath, true);
            }
        }

        [Test]
        public async Task LoadMap_WithValidDmmAndDmFiles_LoadsMapCorrectly()
        {
            // Arrange
            var dmContent = @"
/turf/floor
/obj/wall
";
            var dmmContent = @"
""a"" = (/turf/floor)
""b"" = (/turf/floor, /obj/wall)

(1, 1, 1) = {""
ab
""}
";
            File.WriteAllText(Path.Combine(_project.GetFullPath(Constants.ScriptsRoot), "types.dm"), dmContent);
            var dmmRelativePath = Path.Combine("maps", "test.dmm");
            var dmmFullPath = _project.GetFullPath(dmmRelativePath);
            File.WriteAllText(dmmFullPath, dmmContent);

            var mockMapData = new Mock<Shared.Json.IMapData>();
            var mockCompiledJson = new Mock<Shared.Json.ICompiledJson>();
            mockMapData.Setup(m => m.Blocks).Returns(new System.Collections.Generic.List<Shared.Json.MapBlockJson> { new Shared.Json.MapBlockJson { X = 1, Y = 1, Z = 1, Width = 2, Height = 1, Cells = new System.Collections.Generic.List<string> { "a", "b" } } });
            mockMapData.Setup(m => m.CellDefinitions).Returns(new System.Collections.Generic.Dictionary<string, Shared.Json.MapCellJson> {
                { "a", new Shared.Json.MapCellJson { Turf = new Shared.Json.MapJsonObjectJson { Type = 0 } } },
                { "b", new Shared.Json.MapCellJson { Turf = new Shared.Json.MapJsonObjectJson { Type = 0 }, Objects = new System.Collections.Generic.List<Shared.Json.MapJsonObjectJson> { new Shared.Json.MapJsonObjectJson { Type = 1 } } } }
            });

            var turfTypeMock = new Mock<Shared.Json.ICompiledTypeJson>();
            turfTypeMock.SetupGet(p => p.Path).Returns("/turf/floor");
            var wallTypeMock = new Mock<Shared.Json.ICompiledTypeJson>();
            wallTypeMock.SetupGet(p => p.Path).Returns("/obj/wall");

            mockCompiledJson.Setup(m => m.Types).Returns(new System.Collections.Generic.List<Shared.Json.ICompiledTypeJson> {
                turfTypeMock.Object,
                wallTypeMock.Object
            });

            _dmmParserServiceMock.Setup(p => p.ParseDmm(It.IsAny<System.Collections.Generic.List<string>>(), dmmFullPath)).Returns((mockMapData.Object, mockCompiledJson.Object));

            // Act
            var map = await _dmmService.LoadMapAsync(dmmFullPath);
            _gameState.Map = map;

            // Assert
            Assert.That(map, Is.Not.Null, "Map should not be null after loading.");

            var turfA = map.GetTurf(0, 0, 0);
            Assert.That(turfA, Is.Not.Null, "Turf at (0,0,0) should exist.");
            Assert.That(turfA.Contents.Count, Is.EqualTo(1), "Turf A should have 1 object (the turf itself).");
            Assert.That(turfA.Contents.First().ObjectType.Name, Is.EqualTo("/turf/floor"));

            var turfB = map.GetTurf(1, 0, 0);
            Assert.That(turfB, Is.Not.Null, "Turf at (1,0,0) should exist.");
            Assert.That(turfB.Contents.Count, Is.EqualTo(2), "Turf B should have 2 objects.");
            Assert.That(turfB.Contents.Any(o => o.ObjectType.Name == "/turf/floor"), Is.True, "Turf B should contain a floor.");
            Assert.That(turfB.Contents.Any(o => o.ObjectType.Name == "/obj/wall"), Is.True, "Turf B should contain a wall.");
        }
    }
}
