namespace Shared
{
    public class ServerSettings
    {
        public string ServerName { get; set; } = "BYOND 2.0 Server";
        public string ServerDescription { get; set; } = "A default server instance.";
        public int MaxPlayers { get; set; } = 32;
        public bool EnableVm { get; set; } = false;
        public int VmMaxInstructions { get; set; } = 1000000;

        public NetworkSettings Network { get; set; } = new();
        public ThreadingSettings Threading { get; set; } = new();
        public PerformanceSettings Performance { get; set; } = new();
        public DevelopmentSettings Development { get; set; } = new();
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
        public int VmInstructionSlice { get; set; } = 100;
        public int SnapshotBroadcastInterval { get; set; } = 100; // ms
        public TimeBudgetSettings TimeBudgeting { get; set; } = new();
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
