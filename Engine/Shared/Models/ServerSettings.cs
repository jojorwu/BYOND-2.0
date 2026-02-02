using Shared.Models;
using Shared.Interfaces;
using Shared.Enums;
using Shared.Operations;
using Shared.Services;
namespace Shared.Models
{
    public class ServerSettings
    {
        public string ServerName { get; set; } = "BYOND 2.0 Server";
        public string ServerDescription { get; set; } = "A default server instance.";
        public int MaxPlayers { get; set; } = 32;
        public bool EnableVm { get; set; } = false;
        public int VmMaxInstructions { get; set; } = 1000000;

        public NetworkSettings Network { get; set; } = new();
        public HttpServerSettings HttpServer { get; set; } = new();
        public ThreadingSettings Threading { get; set; } = new();
        public PerformanceSettings Performance { get; set; } = new();
        public DevelopmentSettings Development { get; set; } = new();
        public string PlayerObjectTypePath { get; set; } = "/obj/player";
    }

    public class HttpServerSettings
    {
        public bool Enabled { get; set; } = true;
        public int Port { get; set; } = 9051;
        public string AssetsRoot { get; set; } = "assets";
    }

    public class NetworkSettings
    {
        public NetworkMode Mode { get; set; } = NetworkMode.Automatic;
        public string IpAddress { get; set; } = "127.0.0.1";
        public int UdpPort { get; set; } = 9050;
        public string ConnectionKey { get; set; } = "BYOND2.0";
        public int DisconnectTimeout { get; set; } = 10000;
    }

    public enum NetworkMode
    {
        Automatic,
        Manual
    }

    public class ThreadingSettings
    {
        public ThreadMode Mode { get; set; } = ThreadMode.Automatic;
        public int ThreadCount { get; set; } = 0; // 0 for auto
    }

    public enum ThreadMode
    {
        Automatic,
        Manual
    }

    public class PerformanceSettings
    {
        public int TickRate { get; set; } = 60;
        public bool EnableRegionalProcessing { get; set; } = false;
        public RegionalProcessingSettings RegionalProcessing { get; set; } = new();
        public int VmInstructionSlice { get; set; } = 100;
        public int SnapshotBroadcastInterval { get; set; } = 100; // ms
        public TimeBudgetSettings TimeBudgeting { get; set; } = new();
    }

    public class RegionalProcessingSettings
    {
        public int RegionSize { get; set; } = 8; // The size of a region in chunks
        public int MaxThreads { get; set; } = 0; // 0 for auto
        public int ActivationRange { get; set; } = 1; // in regions
        public int ZActivationRange { get; set; } = 0; // in regions, 0 means only the current z-level
        public bool EnableRegionMerging { get; set; } = false;
        public int MinRegionsToMerge { get; set; } = 2;
        public int ScriptActiveRegionTimeout { get; set; } = 60; // in seconds
    }

    public class TimeBudgetSettings
    {
        public ScriptHostBudgetSettings ScriptHost { get; set; } = new();
    }

    public class ScriptHostBudgetSettings
    {
        public bool Enabled { get; set; } = true;
        public double BudgetPercent { get; set; } = 0.5; // 50% of tick time
    }

    public class DevelopmentSettings
    {
        public int ScriptReloadDebounceMs { get; set; } = 200;
    }
}
