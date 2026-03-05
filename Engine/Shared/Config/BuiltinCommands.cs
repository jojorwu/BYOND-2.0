using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Shared.Config;

public class CVarListCommand : IConsoleCommand
{
    private readonly IConfigurationManager _manager;
    public string Command => "cvar_list";
    public string Description => "Lists all registered configuration variables.";
    public string Help => "Usage: cvar_list [filter]";

    public CVarListCommand(IConfigurationManager manager) => _manager = manager;

    public Task<string> Execute(string[] args)
    {
        var filter = args.Length > 0 ? args[0].ToLowerInvariant() : null;
        var cvars = _manager.GetRegisteredCVars()
            .Where(c => filter == null || c.Name.ToLowerInvariant().Contains(filter))
            .OrderBy(c => c.Name);

        var sb = new StringBuilder();
        sb.AppendLine("Registered CVars:");
        foreach (var c in cvars)
        {
            sb.AppendLine($"  {c.Name} = {c.Value} ({c.Type.Name})");
        }
        return Task.FromResult(sb.ToString());
    }
}

public class CVarSetCommand : IConsoleCommand
{
    private readonly IConfigurationManager _manager;
    public string Command => "cvar_set";
    public string Description => "Sets the value of a configuration variable.";
    public string Help => "Usage: cvar_set <name> <value>";

    public CVarSetCommand(IConfigurationManager manager) => _manager = manager;

    public Task<string> Execute(string[] args)
    {
        if (args.Length < 2) return Task.FromResult(Help);
        var name = args[0];
        var value = args[1];

        try
        {
            if (_manager is ConfigurationManager mgr) mgr.SetCVarDirect(name, value);
            else _manager.SetCVar(name, (object)value);
            return Task.FromResult($"CVar '{name}' set to '{value}'.");
        }
        catch (System.Exception ex)
        {
            return Task.FromResult($"Failed to set CVar '{name}': {ex.Message}");
        }
    }
}

public class HelpCommand : IConsoleCommand
{
    private readonly IConsoleCommandManager _manager;
    public string Command => "help";
    public string Description => "Displays help for all available commands.";
    public string Help => "Usage: help [command]";

    public HelpCommand(IConsoleCommandManager manager) => _manager = manager;

    public Task<string> Execute(string[] args)
    {
        if (args.Length > 0)
        {
            var cmd = _manager.GetAvailableCommands().FirstOrDefault(c => c.Command.Equals(args[0], System.StringComparison.OrdinalIgnoreCase));
            if (cmd != null)
            {
                return Task.FromResult($"{cmd.Command}: {cmd.Description}\n{cmd.Help}");
            }
            return Task.FromResult($"Unknown command: {args[0]}");
        }

        var sb = new StringBuilder();
        sb.AppendLine("Available commands:");
        foreach (var cmd in _manager.GetAvailableCommands().OrderBy(c => c.Command))
        {
            sb.AppendLine($"  {cmd.Command,-15} - {cmd.Description}");
        }
        return Task.FromResult(sb.ToString());
    }
}

public class SoundPlayCommand : IConsoleCommand
{
    private readonly ISoundApi _soundApi;
    public string Command => "play_sound";
    public string Description => "Plays a sound file or named sound.";
    public string Help => "Usage: play_sound <file_or_name> [volume] [pitch]";

    public SoundPlayCommand(ISoundApi soundApi) => _soundApi = soundApi;

    public Task<string> Execute(string[] args)
    {
        if (args.Length < 1) return Task.FromResult(Help);

        string target = args[0];
        float volume = args.Length > 1 && float.TryParse(args[1], out var v) ? v : 100f;
        float pitch = args.Length > 2 && float.TryParse(args[2], out var p) ? p : 1f;

        // Try playing as named first, then as file
        _soundApi.PlayNamed(target);
        _soundApi.Play(target, volume, pitch);

        return Task.FromResult($"Played sound: {target}");
    }
}

public class StatusCommand : IConsoleCommand
{
    private readonly string _serverName;
    private readonly int _maxPlayers;
    private readonly IPlayerManager _playerManager;

    public string Command => "status";
    public string Description => "Displays server status and performance metrics.";
    public string Help => "Usage: status";

    public StatusCommand(string serverName, int maxPlayers, IPlayerManager playerManager)
    {
        _serverName = serverName;
        _maxPlayers = maxPlayers;
        _playerManager = playerManager;
    }

    public Task<string> Execute(string[] args)
    {
        var sb = new StringBuilder();
        sb.AppendLine("Server Status:");
        sb.AppendLine($"  Server Name: {_serverName}");

        int playerCount = 0;
        _playerManager.ForEachPlayer(_ => playerCount++);
        sb.AppendLine($"  Players:     {playerCount}/{_maxPlayers}");

        return Task.FromResult(sb.ToString());
    }
}

public class PlayerListCommand : IConsoleCommand
{
    private readonly IPlayerManager _playerManager;
    public string Command => "list_players";
    public string Description => "Lists all connected players.";
    public string Help => "Usage: list_players";

    public PlayerListCommand(IPlayerManager playerManager) => _playerManager = playerManager;

    public Task<string> Execute(string[] args)
    {
        var sb = new StringBuilder();
        sb.AppendLine("Connected Players:");
        _playerManager.ForEachPlayer(p => {
            sb.AppendLine($"  - {p.Nickname ?? "Unknown"} ({p.EndPoint})");
        });
        return Task.FromResult(sb.ToString());
    }
}
