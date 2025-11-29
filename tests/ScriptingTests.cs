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
        private MapLoader _mapLoader = null!;
        private Project _project = null!;
        private string _projectPath = null!;

        [SetUp]
        public void SetUp()
        {
            _projectPath = Path.Combine(Path.GetTempPath(), Path.GetRandomFileName());
            Directory.CreateDirectory(_projectPath);
            _project = new Project(_projectPath);

            _gameState = new GameState();
            _objectTypeManager = new ObjectTypeManager();
            _mapLoader = new MapLoader(_objectTypeManager);
            _gameApi = new GameApi(_project, _gameState, _objectTypeManager, _mapLoader);
            _scripting = new Scripting(_gameApi);
        }

        [TearDown]
        public void TearDown()
        {
            _scripting.Dispose();
            if (Directory.Exists(_projectPath))
            {
                Directory.Delete(_projectPath, true);
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
        public void NewInstance_ShouldHaveCleanState()
        {
            // Arrange: Define a variable in the first Lua state
            _scripting.ExecuteString("testVar = 123");
            Assert.That(_scripting.lua["testVar"], Is.EqualTo(123.0)); // NLua reads numbers as doubles

            // Act: Dispose the old instance and create a new one
            _scripting.Dispose();
            _scripting = new Scripting(_gameApi);

            // Assert: The new Lua state should not have the variable from the old one
            Assert.That(_scripting.lua["testVar"], Is.Null);
        }

        [Test]
        public void Lua_CannotCallWriteScriptFile()
        {
            string script = @"Game:WriteScriptFile('test.lua', 'print(\'hello\')')";
            Assert.Throws<NLua.Exceptions.LuaScriptException>(() => _scripting.ExecuteString(script));
        }

        [Test]
        public void Lua_CannotCallDeleteScriptFile()
        {
            string script = @"Game:DeleteScriptFile('test.lua')";
            Assert.Throws<NLua.Exceptions.LuaScriptException>(() => _scripting.ExecuteString(script));
        }

        [Test]
        public void ExecuteString_WithInfiniteLoop_ShouldThrowExceptionWithTimeoutMessage()
        {
            var script = "while true do end";
            var ex = Assert.Throws<Exception>(() => _scripting.ExecuteString(script));
            Assert.That(ex.Message, Is.EqualTo("Script execution timed out."));
        }
    }
}
