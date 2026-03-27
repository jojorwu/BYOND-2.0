using System.Diagnostics;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Microsoft.Extensions.Logging.Abstractions;
using Shared;
using Shared.Interfaces;
using Shared.Services;
using Server;
using Core;
using Core.Regions;
using Robust.Shared.Maths;
using System.Collections.Concurrent;
using Moq;

namespace Benchmarks;

public class ChunkLoadingBenchmark
{
    private class MockPlayerManager : IPlayerManager
    {
        public List<IGameObject> Players = new();
        public void ForEachPlayerObject(Action<IGameObject> action)
        {
            foreach (var p in Players) action(p);
        }
        public void ForEachPlayer(Action<INetworkPeer> action) { }
        public void ForEachPlayerInRegion(Region region, Action<INetworkPeer> action) { }
        public void AddPlayer(INetworkPeer peer) { }
        public void RemovePlayer(INetworkPeer peer) { }
    }

    public static async Task RunAsync()
    {
        Console.WriteLine("Starting BYOND 2.0 Chunk & Region Loading Benchmark...");

        var settings = new ServerSettings();
        settings.Performance.EnableRegionalProcessing = true;
        settings.Performance.RegionalProcessing.RegionSize = 4; // 128x128 units
        settings.Performance.RegionalProcessing.ActivationRange = 1; // Covers ~128 units around player (closest to "100" requested)
        settings.Network.EnableBinarySnapshots = true;

        var services = new ServiceCollection();
        services.AddSingleton(Options.Create(settings));
        services.AddSingleton<IComputeService, ComputeService>();
        services.AddSingleton<IJobSystem, JobSystem>(sp => new JobSystem(NullLogger<JobSystem>.Instance, TimeProvider.System, MockDiagnosticBus.Instance));
        services.AddSingleton<SpatialGrid>(sp => new SpatialGrid(NullLogger<SpatialGrid>.Instance, TimeProvider.System, MockDiagnosticBus.Instance));
        services.AddSingleton<IGameState, GameState>();
        services.AddSingleton<IMap, Map>();
        services.AddSingleton<IRegionManager, RegionManager>();
        services.AddSingleton<IPlayerManager, MockPlayerManager>();
        services.AddSingleton<IRegionActivationStrategy, PlayerBasedActivationStrategy>();

        var mockScriptHost = new Mock<IScriptHost>();
        mockScriptHost.Setup(s => s.GetThreads()).Returns(new List<IScriptThread>());
        mockScriptHost.Setup(s => s.ExecuteThreadsAsync(It.IsAny<IEnumerable<IScriptThread>>(), It.IsAny<IEnumerable<IGameObject>>(), It.IsAny<bool>(), It.IsAny<HashSet<long>>(), It.IsAny<bool>()))
            .ReturnsAsync(new List<IScriptThread>());

        services.AddSingleton<IScriptHost>(mockScriptHost.Object);

        var mockUdpServer = new Mock<IUdpServer>();
        services.AddSingleton<IUdpServer>(mockUdpServer.Object);

        services.AddSingleton<BinarySnapshotService>();
        services.AddSingleton<IGameStateSnapshotter, GameStateSnapshotter>();

        var provider = services.BuildServiceProvider();

        var gameState = provider.GetRequiredService<IGameState>();
        var map = provider.GetRequiredService<IMap>();
        var regionManager = provider.GetRequiredService<IRegionManager>();
        var playerManager = (MockPlayerManager)provider.GetRequiredService<IPlayerManager>();
        var strategy = new RegionalGameLoopStrategy(
            provider.GetRequiredService<IScriptHost>(),
            regionManager,
            provider.GetRequiredService<IRegionActivationStrategy>(),
            provider.GetRequiredService<IUdpServer>(),
            gameState,
            provider.GetRequiredService<IGameStateSnapshotter>(),
            provider.GetRequiredService<IJobSystem>(),
            provider.GetRequiredService<IOptions<ServerSettings>>()
        );

        // Pre-fill map 1000x1000 with some turfs to simulate load
        Console.WriteLine("Initializing 1000x1000 map...");
        for (long x = 0; x < 1000; x += 32)
        {
            for (long y = 0; y < 1000; y += 32)
            {
                map.SetTurfType(x, y, 0, 1);
            }
        }
        regionManager.Initialize();

        var playerType = new ObjectType(1, "player");
        var objType = new ObjectType(2, "obj");
        var rand = new Random(42);

        var activePlayers = new Queue<(GameObject Player, List<GameObject> Nearby)>();

        Console.WriteLine("Running 30s simulation...");
        var sw = Stopwatch.StartNew();
        var tickSw = new Stopwatch();

        long totalTickTime = 0;
        int ticks = 0;

        for (int sec = 0; sec < 30; sec++)
        {
            // 1. Remove oldest player if 5 already exist
            if (activePlayers.Count >= 5)
            {
                var (oldPlayer, nearby) = activePlayers.Dequeue();
                gameState.RemoveGameObject(oldPlayer);
                playerManager.Players.Remove(oldPlayer);
                foreach (var o in nearby) gameState.RemoveGameObject(o);
            }

            // 2. Spawn new player at random location
            long px = rand.Next(0, 1000);
            long py = rand.Next(0, 1000);
            var player = new GameObject(playerType);
            player.SetPosition(px, py, 0);
            gameState.AddGameObject(player);
            playerManager.Players.Add(player);

            var nearbyObjs = new List<GameObject>();
            for (int i = 0; i < 10; i++)
            {
                var obj = new GameObject(objType);
                obj.SetPosition(px + rand.Next(-10, 10), py + rand.Next(-10, 10), 0);
                gameState.AddGameObject(obj);
                nearbyObjs.Add(obj);
            }
            activePlayers.Enqueue((player, nearbyObjs));

            // 3. Simulate multiple ticks per second (e.g. 20 ticks)
            for (int t = 0; t < 20; t++)
            {
                tickSw.Restart();
                await strategy.TickAsync(CancellationToken.None);
                tickSw.Stop();
                totalTickTime += tickSw.ElapsedMilliseconds;
                ticks++;
            }

            if (sec % 5 == 0)
            {
                Console.WriteLine($"Seconds: {sec}, Avg Tick Time: {(double)totalTickTime / ticks:F2}ms, Active Regions: {provider.GetRequiredService<IRegionActivationStrategy>().GetActiveRegions().Count}");
            }
        }

        sw.Stop();
        Console.WriteLine("\nBenchmark Complete.");
        Console.WriteLine($"Total Simulation Time: {sw.Elapsed.TotalSeconds:F2}s");
        Console.WriteLine($"Average Regional Tick Time: {(double)totalTickTime / ticks:F2}ms");
        Console.WriteLine($"Total Ticks Processed: {ticks}");
        Console.WriteLine($"Objects in World: {gameState.GameObjects.Count}");
    }
}
