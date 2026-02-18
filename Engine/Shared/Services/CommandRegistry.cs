using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using Shared.Interfaces;

namespace Shared.Services
{
    public class CommandRegistry : ICommandRegistry
    {
        private readonly ConcurrentDictionary<string, ICommandHandler> _handlers = new();

        public void RegisterHandler(ICommandHandler handler)
        {
            _handlers[handler.CommandName.ToLower()] = handler;
        }

        public void UnregisterHandler(string commandName)
        {
            _handlers.TryRemove(commandName.ToLower(), out _);
        }

        public string? ExecuteCommand(string commandLine)
        {
            if (string.IsNullOrWhiteSpace(commandLine)) return null;

            var parts = commandLine.Split(' ', StringSplitOptions.RemoveEmptyEntries);
            if (parts.Length == 0) return null;

            string commandName = parts[0].ToLower();
            string[] args = parts.Skip(1).ToArray();

            if (_handlers.TryGetValue(commandName, out var handler))
            {
                try
                {
                    return handler.Execute(args);
                }
                catch (Exception ex)
                {
                    return $"Error executing command '{commandName}': {ex.Message}";
                }
            }

            return null; // Not handled by registry
        }
    }
}
