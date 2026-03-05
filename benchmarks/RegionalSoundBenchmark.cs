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
using Core.Api;
using Moq;

namespace Benchmarks;

public class RegionalSoundBenchmark
{
    private class MockPlayerManager : IPlayerManager
    {
        public List<INetworkPeer> AllPeers = new();
        public Dictionary<Region, List<INetworkPeer>> RegionPeers = new();

        public void ForEachPlayerObject(Action<IGameObject> action) { }
        public void ForEachPlayer(Action<INetworkPeer> action) => AllPeers.ForEach(action);
        public void ForEachPlayerInRegion(Region region, Action<INetworkPeer> action)
        {
            if (RegionPeers.TryGetValue(region, out var peers)) peers.ForEach(action);
        }
        public void AddPlayer(INetworkPeer peer) { }
        public void RemovePlayer(INetworkPeer peer) { }
    }

    public static void Run()
    {
        Console.WriteLine("Starting BYOND 2.0 Regional Sound Isolation Benchmark...");

        var settings = new ServerSettings();
        settings.Performance.RegionalProcessing.RegionSize = 8;

        var services = new ServiceCollection();
        services.AddSingleton(Options.Create(settings));
        services.AddSingleton<IGameState, GameState>();
        services.AddSingleton<IMap, Map>();
        services.AddSingleton<IRegionManager, RegionManager>();

        var playerManager = new MockPlayerManager();
        services.AddSingleton<IPlayerManager>(playerManager);

        var mockNetwork = new Mock<INetworkService>();
        services.AddSingleton<INetworkService>(mockNetwork.Object);

        var mockUdp = new Mock<IUdpServer>();
        services.AddSingleton<IUdpServer>(mockUdp.Object);
        services.AddSingleton<BinarySnapshotService>();
        services.AddSingleton<ISoundApi, SoundApi>();

        var provider = services.BuildServiceProvider();
        var regionManager = provider.GetRequiredService<IRegionManager>();
        var map = provider.GetRequiredService<IMap>();
        var mockUdpServer = provider.GetRequiredService<IUdpServer>();

        // Initialize map with regions
        map.SetTurfType(0, 0, 0, 1);
        map.SetTurfType(1000, 1000, 0, 1);
        regionManager.Initialize();

        var regionNear = regionManager.GetRegions(0).First(r => r.Coords == (0, 0));
        var regionFar = regionManager.GetRegions(0).First(r => r.Coords == (1000/256, 1000/256));

        var peerNear = new Mock<INetworkPeer>();
        var peerFar = new Mock<INetworkPeer>();

        playerManager.AllPeers.Add(peerNear.Object);
        playerManager.AllPeers.Add(peerFar.Object);
        playerManager.RegionPeers[regionNear] = new List<INetworkPeer> { peerNear.Object };
        playerManager.RegionPeers[regionFar] = new List<INetworkPeer> { peerFar.Object };

        var soundApi = provider.GetRequiredService<ISoundApi>();

        Console.WriteLine("Executing 1,000 regional sound plays...");
        var sw = Stopwatch.StartNew();
        for (int i = 0; i < 1000; i++)
        {
            soundApi.PlayAt("test.ogg", 0, 0, 0);
        }
        sw.Stop();

        Console.WriteLine($"Benchmark Complete.");
        Console.WriteLine($"Time taken: {sw.ElapsedMilliseconds}ms");

        Mock.Get(mockUdpServer).Verify(u => u.BroadcastSound(It.IsAny<SoundData>(), regionNear), Times.Exactly(1000));
        Mock.Get(mockUdpServer).Verify(u => u.BroadcastSound(It.IsAny<SoundData>(), regionFar), Times.Never());

        Console.WriteLine("Verification Successful: 1000 sounds sent to near peer, 0 to far peer.");
    }
}
