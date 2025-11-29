namespace Core
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
    }

    public class NetworkSettings
    {
        public NetworkMode Mode { get; set; } = NetworkMode.Automatic;
        public string IpAddress { get; set; } = "127.0.0.1";
        public int Port { get; set; } = 7777;
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
        public int TickRate { get; set; } = 20;
        public int VmInstructionSlice { get; set; } = 100;
        public int SubsystemTickBudget { get; set; } = 10;
    }
}
