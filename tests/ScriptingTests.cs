using NUnit.Framework;
using Core;
using System;
using System.IO;

namespace Core.Tests
{
    [TestFixture]
    public class ScriptingTests
    {
        private Scripting _scripting = null!;
        private GameApi _gameApi = null!;
        private GameState _gameState = null!;
        private ObjectTypeManager _objectTypeManager = null!;
        private const string TestProjectDir = "TestProject";
        private Project _project = null!;
        private MapLoader _mapLoader = null!;

        [SetUp]
        public void SetUp()
        {
            Directory.CreateDirectory(TestProjectDir);
            _project = new Project(TestProjectDir);
            _gameState = new GameState();
            _objectTypeManager = new ObjectTypeManager(_project);
            _mapLoader = new MapLoader(_objectTypeManager, _project);
            _gameApi = new GameApi(_gameState, _objectTypeManager, _mapLoader, _project);
            _scripting = new Scripting(_gameApi);
        }

        [TearDown]
        public void TearDown()
        {
            _scripting.Dispose();
            if (Directory.Exists(TestProjectDir))
            {
                Directory.Delete(TestProjectDir, true);
            }
        }

        [Test]
        public void ExecuteFile_WithNullPath_ShouldThrowArgumentNullException()
        {
            Assert.Throws<ArgumentNullException>(() => _scripting.ExecuteFile(null));
        }

        [Test]
        public void ExecuteFile_WithInvalidPath_ShouldThrowFileNotFoundException()
        {
            Assert.Throws<FileNotFoundException>(() => _scripting.ExecuteFile("nonexistent.lua"));
        }

        [Test]
        public void ExecuteFile_WithValidScript_ShouldExecuteSuccessfully()
        {
            var tempFile = Path.GetTempFileName();
            File.WriteAllText(tempFile, "print('test')");
            Assert.DoesNotThrow(() => _scripting.ExecuteFile(tempFile));
            File.Delete(tempFile);
        }

        [Test]
        public void Reload_ShouldResetLuaState()
        {
            var tempFile = Path.GetTempFileName();
            File.WriteAllText(tempFile, "testVar = 123");
            _scripting.ExecuteFile(tempFile);
            _scripting.Reload();
            File.WriteAllText(tempFile, "if testVar == nil then print('testVar is nil') else print('testVar is not nil') end");
            Assert.DoesNotThrow(() => _scripting.ExecuteFile(tempFile));
            File.Delete(tempFile);
        }
    }
}
