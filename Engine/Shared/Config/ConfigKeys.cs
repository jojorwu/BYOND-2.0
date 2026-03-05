namespace Shared.Config;

public static class ConfigKeys
{
    [CVar(GridCellSize, CVarFlags.Archive, "The size of a grid cell.")]
    public const string GridCellSize = "Engine.Grid.CellSize";

    [CVar(MaxThreads, CVarFlags.Archive, "The maximum number of threads for the job system.")]
    public const string MaxThreads = "Engine.JobSystem.MaxThreads";

    [CVar(TickRate, CVarFlags.Archive, "The engine tick rate in milliseconds.")]
    public const string TickRate = "Engine.TickRate";

    [CVar(NetworkPort, CVarFlags.Archive, "The network port.")]
    public const string NetworkPort = "Network.Port";

    public const string NetworkTimeout = "Network.Timeout";
    public const string ProfilingEnabled = "Engine.Profiling.Enabled";

    // Graphics Settings
    [CVar(GraphicsBloomEnabled, CVarFlags.Archive | CVarFlags.Client, "Enables or disables bloom effects.", "Graphics")]
    public const string GraphicsBloomEnabled = "Graphics.BloomEnabled";

    [CVar(GraphicsSSAOEnabled, CVarFlags.Archive | CVarFlags.Client, "Enables or disables Screen Space Ambient Occlusion.", "Graphics")]
    public const string GraphicsSSAOEnabled = "Graphics.SSAOEnabled";

    [CVar(GraphicsResolutionX, CVarFlags.Archive | CVarFlags.Client, "Horizontal window resolution.", "Graphics")]
    public const string GraphicsResolutionX = "Graphics.ResolutionX";

    [CVar(GraphicsResolutionY, CVarFlags.Archive | CVarFlags.Client, "Vertical window resolution.", "Graphics")]
    public const string GraphicsResolutionY = "Graphics.ResolutionY";

    [CVar(GraphicsVSync, CVarFlags.Archive | CVarFlags.Client, "Enables or disables vertical synchronization.", "Graphics")]
    public const string GraphicsVSync = "Graphics.VSync";

    // Server Settings
    [CVar(ServerName, CVarFlags.Archive | CVarFlags.Server | CVarFlags.Replicated, "The name of the server.", "Server")]
    public const string ServerName = "Server.Name";

    [CVar(ServerMaxPlayers, CVarFlags.Archive | CVarFlags.Server, "The maximum number of players allowed.", "Server")]
    public const string ServerMaxPlayers = "Server.MaxPlayers";

    [CVar(ServerPort, CVarFlags.Archive | CVarFlags.Server, "The port for UDP communication.", "Server")]
    public const string ServerPort = "Server.Port";
}
