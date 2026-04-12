using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

using Shared.Services;
using Shared.Attributes;

namespace Shared.Config;

public interface IConsoleCommandManager
{
    void RegisterCommand(IConsoleCommand command);
    Task<string> ExecuteCommand(string input);
    IEnumerable<IConsoleCommand> GetAvailableCommands();
}

[EngineService(typeof(IConsoleCommandManager))]
public class ConsoleCommandManager : EngineService, IConsoleCommandManager
{
    private readonly ConcurrentDictionary<string, IConsoleCommand> _commands = new();
    private readonly IConfigurationManager _config;
    private readonly ISoundApi _soundApi;
    private readonly IPlayerManager _playerManager;
    private readonly ServerSettings _settings;

    public ConsoleCommandManager(IConfigurationManager config, ISoundApi soundApi, IPlayerManager playerManager, ServerSettings settings)
    {
        _config = config;
        _soundApi = soundApi;
        _playerManager = playerManager;
        _settings = settings;
    }

    protected override Task OnInitializeAsync()
    {
        RegisterCommand(new CVarListCommand(_config));
        RegisterCommand(new CVarSetCommand(_config));
        RegisterCommand(new HelpCommand(this));
        RegisterCommand(new SoundPlayCommand(_soundApi));
        RegisterCommand(new StatusCommand(_settings.ServerName, _settings.MaxPlayers, _playerManager));
        RegisterCommand(new PlayerListCommand(_playerManager));
        return Task.CompletedTask;
    }

    public void RegisterCommand(IConsoleCommand command)
    {
        _commands[command.Command.ToLowerInvariant()] = command;
    }

    public async Task<string> ExecuteCommand(string input)
    {
        if (string.IsNullOrWhiteSpace(input)) return "";

        var parts = input.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        var cmdName = parts[0].ToLowerInvariant();
        var args = parts.Skip(1).ToArray();

        if (_commands.TryGetValue(cmdName, out var command))
        {
            try
            {
                return await command.Execute(args);
            }
            catch (Exception ex)
            {
                return $"Error executing command '{cmdName}': {ex.Message}";
            }
        }

        return $"Unknown command: {cmdName}. Type 'help' for available commands.";
    }

    public IEnumerable<IConsoleCommand> GetAvailableCommands() => _commands.Values;
}
