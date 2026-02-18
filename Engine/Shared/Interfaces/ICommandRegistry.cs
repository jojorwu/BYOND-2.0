using System;

namespace Shared.Interfaces
{
    public interface ICommandHandler
    {
        string CommandName { get; }
        string Description { get; }
        string Execute(string[] args);
    }

    public interface ICommandRegistry
    {
        void RegisterHandler(ICommandHandler handler);
        void UnregisterHandler(string commandName);
        string? ExecuteCommand(string commandLine);
    }
}
