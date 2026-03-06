namespace Shared.Config;

public static class ConfigKeys
{
    public static readonly CVarDef<long> GridCellSize = new("Engine.Grid.CellSize", 32L, CVarFlags.Archive, "The size of a grid cell.");
    public static readonly CVarDef<int> MaxThreads = new("Engine.JobSystem.MaxThreads", 0, CVarFlags.Archive, "The maximum number of threads for the job system. 0 = Auto.");
    public static readonly CVarDef<double> TickRate = new("Engine.TickRate", 16.66, CVarFlags.Archive, "The engine tick rate in milliseconds.");

    public static readonly CVarDef<int> NetworkPort = new("Network.Port", 1212, CVarFlags.Archive, "The network port.");
    public static readonly CVarDef<int> NetworkTimeout = new("Network.Timeout", 30, CVarFlags.Archive, "Network connection timeout in seconds.");
    public static readonly CVarDef<bool> ProfilingEnabled = new("Engine.Profiling.Enabled", false, CVarFlags.Archive, "Enables engine-wide profiling.");

    // Graphics Settings
    public static readonly CVarDef<bool> GraphicsBloomEnabled = new("Graphics.BloomEnabled", true, CVarFlags.Archive | CVarFlags.Client, "Enables or disables bloom effects.", "Graphics");
    public static readonly CVarDef<bool> GraphicsSSAOEnabled = new("Graphics.SSAOEnabled", true, CVarFlags.Archive | CVarFlags.Client, "Enables or disables Screen Space Ambient Occlusion.", "Graphics");
    public static readonly CVarDef<int> GraphicsResolutionX = new("Graphics.ResolutionX", 1280, CVarFlags.Archive | CVarFlags.Client, "Horizontal window resolution.", "Graphics");
    public static readonly CVarDef<int> GraphicsResolutionY = new("Graphics.ResolutionY", 720, CVarFlags.Archive | CVarFlags.Client, "Vertical window resolution.", "Graphics");
    public static readonly CVarDef<bool> GraphicsVSync = new("Graphics.VSync", true, CVarFlags.Archive | CVarFlags.Client, "Enables or disables vertical synchronization.", "Graphics");
    public static readonly CVarDef<bool> GraphicsShadowsEnabled = new("Graphics.ShadowsEnabled", true, CVarFlags.Archive | CVarFlags.Client, "Enables or disables real-time shadows.", "Graphics");
    public static readonly CVarDef<int> GraphicsTextureQuality = new("Graphics.TextureQuality", 2, CVarFlags.Archive | CVarFlags.Client, "Texture filtering quality (0=Point, 1=Bilinear, 2=Trilinear).", "Graphics");

    // Server Settings
    public static readonly CVarDef<string> ServerName = new("Server.Name", "BYOND 2.0 Server", CVarFlags.Archive | CVarFlags.Server | CVarFlags.Replicated, "The name of the server.", "Server");
    public static readonly CVarDef<int> ServerMaxPlayers = new("Server.MaxPlayers", 32, CVarFlags.Archive | CVarFlags.Server, "The maximum number of players allowed.", "Server");
    public static readonly CVarDef<int> ServerPort = new("Server.Port", 1212, CVarFlags.Archive | CVarFlags.Server, "The port for UDP communication.", "Server");

    // Replicated Gameplay Settings (Server-enforced)
    public static readonly CVarDef<double> GameGravity = new("Game.Gravity", 9.81, CVarFlags.Archive | CVarFlags.Server | CVarFlags.Replicated, "World gravity constant.", "Gameplay");
    public static readonly CVarDef<bool> GameFriendlyFire = new("Game.FriendlyFire", true, CVarFlags.Archive | CVarFlags.Server | CVarFlags.Replicated, "Whether players can damage teammates.", "Gameplay");

    // Server-enforced Graphics Defaults
    public static readonly CVarDef<bool> ServerForceShadows = new("Server.Graphics.ForceShadows", false, CVarFlags.Archive | CVarFlags.Server | CVarFlags.Replicated, "Whether the server requires shadows to be enabled for all clients.", "Server Enforced");
    public static readonly CVarDef<int> ServerMaxViewDistance = new("Server.Graphics.MaxViewDistance", 15, CVarFlags.Archive | CVarFlags.Server | CVarFlags.Replicated, "The maximum view distance (in tiles) allowed by the server.", "Server Enforced");
}
