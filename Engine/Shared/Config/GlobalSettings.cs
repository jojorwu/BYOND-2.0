using Shared.Config;

namespace Shared;

public class GlobalSettings
{
    private readonly IConfigurationManager _manager;

    public GlobalSettings(IConfigurationManager manager)
    {
        _manager = manager;
        RegisterAll();
    }

    private void RegisterAll()
    {
        // Graphics
        _manager.RegisterCVar(ConfigKeys.GraphicsBloomEnabled, true, CVarFlags.Archive | CVarFlags.Client, "Enables or disables bloom effects.");
        _manager.RegisterCVar(ConfigKeys.GraphicsSSAOEnabled, true, CVarFlags.Archive | CVarFlags.Client, "Enables or disables Screen Space Ambient Occlusion.");
        _manager.RegisterCVar(ConfigKeys.GraphicsResolutionX, 1280, CVarFlags.Archive | CVarFlags.Client, "Horizontal window resolution.");
        _manager.RegisterCVar(ConfigKeys.GraphicsResolutionY, 720, CVarFlags.Archive | CVarFlags.Client, "Vertical window resolution.");
        _manager.RegisterCVar(ConfigKeys.GraphicsVSync, true, CVarFlags.Archive | CVarFlags.Client, "Enables or disables vertical synchronization.");

        // Server
        _manager.RegisterCVar(ConfigKeys.ServerName, "BYOND 2.0 Server", CVarFlags.Archive | CVarFlags.Server, "The name of the server.");
        _manager.RegisterCVar(ConfigKeys.ServerMaxPlayers, 65536, CVarFlags.Archive | CVarFlags.Server, "The maximum number of players allowed.");
        _manager.RegisterCVar(ConfigKeys.ServerPort, 9050, CVarFlags.Archive | CVarFlags.Server, "The port for UDP communication.");
    }
}
