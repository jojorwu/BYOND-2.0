using Shared;
using NUnit.Framework;
using Core;
using System;
using System.IO;
using System.Linq;
using Moq;
using Core.VM.Runtime;
using Server;
using Shared.Services;
using Core.Maps;
using Core.Api;
using Core.VM.Procs;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

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
        private IScriptApi _scriptApi = null!;

        [SetUp]
        public void SetUp()
        {
            var projectPath = Path.Combine(Path.GetTempPath(), Path.GetRandomFileName());
            Directory.CreateDirectory(projectPath);
            var scriptsPath = Path.Combine(projectPath, "scripts");
            Directory.CreateDirectory(scriptsPath);

            // Create a dummy compiled json file for DmSystem to load
            var compiledJson = new Shared.Compiler.CompiledJson { Strings = new(), Types = Array.Empty<Shared.Compiler.DreamTypeJson>(), Procs = Array.Empty<Shared.Compiler.ProcDefinitionJson>() };
            var jsonContent = System.Text.Json.JsonSerializer.Serialize(compiledJson);
            File.WriteAllText(Path.Combine(projectPath, "project.compiled.json"), jsonContent);
            File.WriteAllText(Path.Combine(projectPath, "project.json"), "{\"scripts_root\": \"scripts\"}");

            _project = new Project(projectPath);
            var pool = new Shared.Services.ObjectPool<GameObject>(() => new GameObject());
            var archetypeManager = new ArchetypeManager(NullLogger<ArchetypeManager>.Instance);
            var componentManager = new ComponentManager(archetypeManager);
            var entityRegistry = new EntityRegistry(pool, componentManager);
            _objectTypeManager = new ObjectTypeManager(NullLogger<ObjectTypeManager>.Instance);
            var objectFactory = new Shared.Services.ObjectFactory(entityRegistry, _objectTypeManager);
            _gameState = new GameState(new SpatialGrid(NullLogger<SpatialGrid>.Instance), objectFactory);
            var jobSystem = new Shared.Services.JobSystem(NullLogger<Shared.Services.JobSystem>.Instance, TimeProvider.System);
            _mapLoader = new MapLoader(_objectTypeManager, objectFactory, jobSystem, NullLogger<MapLoader>.Instance);
            _dreamVM = new DreamVM(Options.Create(new DreamVmConfiguration()), NullLogger<DreamVM>.Instance, new INativeProcProvider[] {
                new MathNativeProcProvider(),
                new SpatialNativeProcProvider(),
                new SystemNativeProcProvider()
            }, objectFactory);
            var mapApi = new MapApi(_gameState, _mapLoader, _project, _objectTypeManager);
            var objectApi = new ObjectApi(_gameState, _objectTypeManager, mapApi, pool, componentManager);
            var spatialQueryApi = new SpatialQueryApi(_gameState, _objectTypeManager, mapApi);
            var standardLibraryApi = new StandardLibraryApi(spatialQueryApi, mapApi);
            var soundApi = new Mock<ISoundApi>().Object;
            var soundRegistry = new Shared.Config.SoundRegistry();
            var commandManager = new Shared.Config.ConsoleCommandManager();
            var timeApi = new Mock<ITimeApi>().Object;
            var eventApi = new Mock<IEventApi>().Object;

            var serviceProviderMock = new Mock<IServiceProvider>();
            var scriptHostMock = new Mock<IScriptHost>();
            serviceProviderMock.Setup(sp => sp.GetService(typeof(IScriptHost))).Returns(scriptHostMock.Object);

            var dreamMakerLoader = new DreamMakerLoader(_objectTypeManager, new CompiledJsonService(new Mock<IGameApi>().Object), _gameState, _dreamVM);
            var loggerMock = new Mock<ILogger<Core.Scripting.DM.DmSystem>>();

            var systems = new IScriptSystem[]
            {
                new Core.Scripting.CSharp.CSharpSystem(new Mock<IGameApi>().Object),
                new Core.Scripting.LuaSystem.LuaSystem(new Mock<IGameApi>().Object),
                new Core.Scripting.DM.DmSystem(_objectTypeManager, dreamMakerLoader, _dreamVM, new Lazy<IScriptHost>(() => serviceProviderMock.Object.GetRequiredService<IScriptHost>()), loggerMock.Object)
            };
            _scriptManager = new ScriptManager(_project, systems, NullLogger<ScriptManager>.Instance);
            _scriptApi = new ScriptApi(_project, _scriptManager);

            var registry = new ApiRegistry();
            var mockSoundApi = new Mock<ISoundApi>();
            mockSoundApi.Setup(s => s.Name).Returns("Sounds");
            var mockTimeApi = new Mock<ITimeApi>();
            mockTimeApi.Setup(t => t.Name).Returns("Time");
            var mockEventApi = new Mock<IEventApi>();
            mockEventApi.Setup(e => e.Name).Returns("Events");

            registry.Register(mapApi);
            registry.Register(objectApi);
            registry.Register(_scriptApi);
            registry.Register(mockSoundApi.Object);
            registry.Register(standardLibraryApi);
            registry.Register(mockTimeApi.Object);
            registry.Register(mockEventApi.Object);

            _gameApi = new GameApi(registry, soundRegistry, commandManager);
        }

        [TearDown]
        public void TearDown()
        {
            _dreamVM.Dispose();
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
            var scriptsPath = Path.Combine(_project.RootPath, "scripts");
            File.WriteAllText(Path.Combine(scriptsPath, "test.lua"), "print('lua loaded')");
            File.WriteAllText(Path.Combine(scriptsPath, "test.dm"), "/mob/player");
            File.WriteAllText(Path.Combine(scriptsPath, "test.cs"), "Console.WriteLine(\"csharp loaded\");");

            // Act & Assert
            Assert.DoesNotThrowAsync(async () => {
                await _scriptManager.InitializeAsync();
            });
        }

        [Test]
        public void ScriptManager_ReloadsScripts()
        {
            // Arrange
            _scriptManager.InitializeAsync().Wait();

            // Act & Assert
            Assert.DoesNotThrowAsync(async () => {
                await _scriptManager.ReloadAll();
            });
        }

        [Test]
        public void ScriptManager_InvokesGlobalEvents()
        {
             // Arrange
            var scriptsPath = Path.Combine(_project.RootPath, "scripts");
            File.WriteAllText(Path.Combine(scriptsPath, "test.lua"), "function MyEvent() print('event handled') end");
            _scriptManager.InitializeAsync().Wait();

            // Act & Assert
            Assert.DoesNotThrow(() => {
                _scriptManager.InvokeGlobalEvent("MyEvent");
            });
        }
    }
}
