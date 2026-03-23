using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

using Shared.Services;

namespace Shared.Config;

public interface IConsoleCommandManager
{
    void RegisterCommand(IConsoleCommand command);
    Task<string> ExecuteCommand(string input);
    IEnumerable<IConsoleCommand> GetAvailableCommands();
}

public class ConsoleCommandManager : EngineService, IConsoleCommandManager
{
    private readonly ConcurrentDictionary<string, IConsoleCommand> _commands = new();

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
