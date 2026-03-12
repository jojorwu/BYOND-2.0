using System;
using System.Threading.Tasks;

namespace Shared.Interfaces;

/// <summary>
/// Represents a game action that can be executed and potentially undone.
/// </summary>
public interface ICommand
{
    string Name { get; }
    Task ExecuteAsync();
}

/// <summary>
/// A command that returns a result.
/// </summary>
public interface ICommand<TResult>
{
    string Name { get; }
    Task<TResult> ExecuteAsync();
}

/// <summary>
/// Context for command execution middleware.
/// </summary>
public class CommandContext
{
    public ICommand Command { get; }
    public object? Result { get; set; }
    public Exception? Exception { get; set; }

    public CommandContext(ICommand command)
    {
        Command = command;
    }
}

/// <summary>
/// Middleware for processing commands in the dispatcher pipeline.
/// </summary>
public interface ICommandMiddleware
{
    Task ProcessAsync(CommandContext context, Func<Task> next);
}
