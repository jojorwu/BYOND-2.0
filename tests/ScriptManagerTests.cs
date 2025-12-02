using NUnit.Framework;
using Core;
using System.IO;
using Core.VM.Runtime;
using Server;
using System.Collections.Generic;
using Core.Scripting;
using Core.Scripting.CSharp;
using Core.Scripting.DM;
using Core.Scripting.LuaSystem;

namespace Core.Tests
{
    [TestFixture]
    public class ScriptManagerTests
    {
        private ScriptManager _scriptManager = null!;
        private Project _project = null!;
        private string _scriptsPath = null!;

        [SetUp]
        public void SetUp()
        {
            var projectPath = Path.Combine(Path.GetTempPath(), Path.GetRandomFileName());
            Directory.CreateDirectory(projectPath);
            _scriptsPath = Path.Combine(projectPath, "scripts");
            Directory.CreateDirectory(_scriptsPath);
            File.WriteAllText(Path.Combine(projectPath, "project.json"), "{\"scripts_root\": \"scripts\"}");

            _project = new Project(projectPath);
            var gameState = new GameState();
            var objectTypeManager = new ObjectTypeManager();
            var mapLoader = new MapLoader(objectTypeManager);
            var dreamVM = new DreamVM(new ServerSettings());

            var mapApi = new MapApi(gameState, mapLoader, _project);
            var objectApi = new ObjectApi(gameState, objectTypeManager, mapApi);
            var stdLibApi = new StandardLibraryApi(gameState, objectTypeManager, mapApi);
            var gameApi = new GameApi(mapApi, objectApi, stdLibApi, _project, gameState);

            var systems = new List<IScriptSystem>
            {
                new CSharpSystem(gameApi),
                new LuaSystem(gameApi),
                new DmSystem(objectTypeManager, _project, dreamVM)
            };
            _scriptManager = new ScriptManager(systems, _project);
        }

        [TearDown]
        public void TearDown()
        {
            if (Directory.Exists(_project.RootPath))
            {
                Directory.Delete(_project.RootPath, true);
            }
        }

        [Test]
        public void ScriptManager_InitializesAndLoadsAllScriptTypes()
        {
            // Arrange
            File.WriteAllText(Path.Combine(_scriptsPath, "test.lua"), "print('lua loaded')");
            File.WriteAllText(Path.Combine(_scriptsPath, "test.dm"), "/mob/player");
            File.WriteAllText(Path.Combine(_scriptsPath, "test.cs"), "System.Console.WriteLine(\"csharp loaded\");");

            // Act & Assert
            Assert.DoesNotThrowAsync(async () => {
                await _scriptManager.Initialize();
            });
        }

        [Test]
        public void ScriptManager_ReloadsScripts()
        {
            // Arrange
            _scriptManager.Initialize();

            // Act & Assert
            Assert.DoesNotThrowAsync(async () => {
                await _scriptManager.ReloadAll();
            });
        }

        [Test]
        public void ScriptManager_InvokesGlobalEvents()
        {
             // Arrange
            File.WriteAllText(Path.Combine(_scriptsPath, "test.lua"), "function MyEvent() print('event handled') end");
            _scriptManager.Initialize();

            // Act & Assert
            Assert.DoesNotThrow(() => {
                _scriptManager.InvokeGlobalEvent("MyEvent");
            });
        }
    }
}
