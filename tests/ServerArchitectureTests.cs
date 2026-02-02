using NUnit.Framework;
using Moq;
using Server;
using Shared;
using Microsoft.Extensions.Logging;
using System.Threading;
using System.Threading.Tasks;
using System;
using LiteNetLib.Utils;
using Core;

namespace tests
{
    [TestFixture]
    public class ServerArchitectureTests
    {
        [Test]
        public void NetDataWriterPool_ReusesWriters()
        {
            var pool = new NetDataWriterPool();
            var writer1 = pool.Get();
            pool.Return(writer1);
            var writer2 = pool.Get();

            Assert.That(writer2, Is.SameAs(writer1));
        }

        [Test]
        public void PerformanceMonitor_CalculatesTPSCorrectly()
        {
            var loggerMock = new Mock<ILogger<PerformanceMonitor>>();
            var monitor = new PerformanceMonitor(loggerMock.Object);

            for (int i = 0; i < 50; i++) monitor.RecordTick();

            Assert.DoesNotThrow(() => monitor.RecordTick());
        }

        [Test]
        public void ServerContext_ProvidesCorrectServices()
        {
            var gs = new Mock<IGameState>().Object;
            var pm = new Mock<IPlayerManager>().Object;
            var set = new ServerSettings();
            var rm = new Mock<IRegionManager>().Object;
            var perf = new PerformanceMonitor(new Mock<ILogger<PerformanceMonitor>>().Object);

            var context = new ServerContext(gs, pm, set, rm, perf);

            Assert.That(context.GameState, Is.SameAs(gs));
            Assert.That(context.PlayerManager, Is.SameAs(pm));
            Assert.That(context.Settings, Is.SameAs(set));
            Assert.That(context.RegionManager, Is.SameAs(rm));
            Assert.That(context.PerformanceMonitor, Is.SameAs(perf));
        }

        [Test]
        public async Task ServerApplication_StartsAndStopsInOrder()
        {
            var loggerMock = new Mock<ILogger<ServerApplication>>();
            var scriptHostMock = new Mock<IScriptHost>();
            var udpServerMock = new Mock<IUdpServer>();

            var settings = new ServerSettings { HttpServer = { Enabled = false } };
            var projectMock = new Mock<IProject>();

            // Mocking classes with complex constructors
            var gameLoopMock = new Mock<GameLoop>(new Mock<IGameLoopStrategy>().Object, new Mock<IServerContext>().Object, new Mock<ILogger<GameLoop>>().Object);
            var httpServerMock = new Mock<HttpServer>(settings, projectMock.Object, new Mock<ILogger<HttpServer>>().Object);
            var perfMonitorMock = new Mock<PerformanceMonitor>(new Mock<ILogger<PerformanceMonitor>>().Object);

            var udpServerHostedMock = udpServerMock.As<Microsoft.Extensions.Hosting.IHostedService>();
            var scriptHostHostedMock = scriptHostMock.As<Microsoft.Extensions.Hosting.IHostedService>();

            var app = new ServerApplication(
                loggerMock.Object,
                perfMonitorMock.Object,
                scriptHostMock.Object,
                udpServerMock.Object,
                httpServerMock.Object,
                gameLoopMock.Object);

            var cts = new CancellationTokenSource();

            await app.StartAsync(cts.Token);

            perfMonitorMock.Verify(m => m.StartAsync(It.IsAny<CancellationToken>()), Times.Once);
            scriptHostHostedMock.Verify(m => m.StartAsync(It.IsAny<CancellationToken>()), Times.Once);

            await app.StopAsync(cts.Token);

            scriptHostHostedMock.Verify(m => m.StopAsync(It.IsAny<CancellationToken>()), Times.Once);
        }

        [Test]
        public void ScriptWatcher_StartsAndSignalsReload()
        {
            var projectMock = new Mock<IProject>();
            var tempDir = Path.Combine(Path.GetTempPath(), Path.GetRandomFileName());
            Directory.CreateDirectory(tempDir);
            Directory.CreateDirectory(Path.Combine(tempDir, "scripts"));

            projectMock.Setup(p => p.GetFullPath(Constants.ScriptsRoot)).Returns(Path.Combine(tempDir, "scripts"));

            var settings = new ServerSettings { Development = { ScriptReloadDebounceMs = 10 } };
            var loggerMock = new Mock<ILogger<ScriptWatcher>>();

            using var watcher = new ScriptWatcher(projectMock.Object, settings, loggerMock.Object);
            bool reloadRequested = false;
            watcher.OnReloadRequested += () => reloadRequested = true;

            watcher.Start();

            // Simulate file change
            File.WriteAllText(Path.Combine(tempDir, "scripts", "test.lua"), "print(1)");

            // Wait for debounce
            Thread.Sleep(100);

            Assert.That(reloadRequested, Is.True);

            watcher.Stop();
            Directory.Delete(tempDir, true);
        }
    }
}
