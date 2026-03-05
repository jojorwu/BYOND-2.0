namespace Shared.Config;

public static class ConfigKeys
{
    public const string GridCellSize = "Engine.Grid.CellSize";
    public const string MaxThreads = "Engine.JobSystem.MaxThreads";
    public const string TickRate = "Engine.TickRate";
    public const string NetworkPort = "Network.Port";
    public const string NetworkTimeout = "Network.Timeout";
    public const string ProfilingEnabled = "Engine.Profiling.Enabled";

    // Graphics Settings
    public const string GraphicsBloomEnabled = "Graphics.BloomEnabled";
    public const string GraphicsSSAOEnabled = "Graphics.SSAOEnabled";
    public const string GraphicsResolutionX = "Graphics.ResolutionX";
    public const string GraphicsResolutionY = "Graphics.ResolutionY";
    public const string GraphicsVSync = "Graphics.VSync";

    // Server Settings
    public const string ServerName = "Server.Name";
    public const string ServerMaxPlayers = "Server.MaxPlayers";
    public const string ServerPort = "Server.Port";
}
