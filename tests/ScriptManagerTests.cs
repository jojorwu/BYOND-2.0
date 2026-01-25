using Shared;
using NUnit.Framework;
using Core;
using System;
using System.IO;
using Moq;
using Core.VM.Runtime;
using Server;
using Core.Objects;
using Core.Maps;
using Core.Api;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace Core.Tests
{
    [TestFixture]
    public class ScriptManagerTests
    {
        private IScriptManager _scriptManager = null!;
        private IGameApi _gameApi = null!;
        private GameState _gameState = null!;
        private ObjectTypeManager _objectTypeManager = null!;
        private MapLoader _mapLoader = null!;
        private Project _project = null!;
        private DreamVM _dreamVM = null!;
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
            _gameState = new GameState();
            _objectTypeManager = new ObjectTypeManager();
            _mapLoader = new MapLoader(_objectTypeManager);
            _dreamVM = new DreamVM(new ServerSettings());
            var mapApi = new MapApi(_gameState, _mapLoader, _project, _objectTypeManager);
            var objectApi = new ObjectApi(_gameState, _objectTypeManager, mapApi);
            var scriptApi = new ScriptApi(_project);
            var spatialQueryApi = new SpatialQueryApi(_gameState, _objectTypeManager, mapApi);
            var standardLibraryApi = new StandardLibraryApi(spatialQueryApi);
            _gameApi = new GameApi(mapApi, objectApi, scriptApi, standardLibraryApi);

            var serviceProviderMock = new Mock<IServiceProvider>();
            var scriptHostMock = new Mock<IScriptHost>();
            serviceProviderMock.Setup(sp => sp.GetService(typeof(IScriptHost))).Returns(scriptHostMock.Object);

            var dreamMakerLoader = new DreamMakerLoader(_objectTypeManager, _project, _dreamVM);
            var compilerService = new DMCompiler.OpenDreamCompilerService();
            var loggerMock = new Mock<ILogger<Core.Scripting.DM.DmSystem>>();

            var systems = new IScriptSystem[]
            {
                new Core.Scripting.CSharp.CSharpSystem(_gameApi),
                new Core.Scripting.LuaSystem.LuaSystem(_gameApi),
                new Core.Scripting.DM.DmSystem(_objectTypeManager, dreamMakerLoader, compilerService, _dreamVM, new Lazy<IScriptHost>(() => serviceProviderMock.Object.GetRequiredService<IScriptHost>()), loggerMock.Object)
            };
            _scriptManager = new ScriptManager(_project, systems);
        }

        [TearDown]
        public void TearDown()
        {
            _gameState.Dispose();
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
            File.WriteAllText(Path.Combine(_scriptsPath, "test.cs"), "Console.WriteLine(\"csharp loaded\");");

            // Act & Assert
            Assert.DoesNotThrow(() => {
                _scriptManager.Initialize();
            });
        }

        [Test]
        public void ScriptManager_ReloadsScripts()
        {
            // Arrange
            _scriptManager.Initialize();

            // Act & Assert
            Assert.DoesNotThrow(() => {
                _scriptManager.ReloadAll();
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
