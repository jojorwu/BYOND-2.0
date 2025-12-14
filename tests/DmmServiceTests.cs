using NUnit.Framework;
using Core;
using System.IO;
using System.Linq;
using Shared;

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
            _dmmService = new DmmService(_objectTypeManager, _project, dreamMakerLoader);
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
        public void LoadMap_WithValidDmmAndDmFiles_LoadsMapCorrectly()
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

            // Act
            var map = _dmmService.LoadMap(dmmFullPath);
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
