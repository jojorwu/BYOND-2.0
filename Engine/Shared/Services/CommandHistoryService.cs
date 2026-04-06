using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Shared.Interfaces;
using Microsoft.Extensions.Logging;
using Shared.Attributes;

namespace Shared.Services;

public interface ICommandHistoryService
{
    bool CanUndo { get; }
    bool CanRedo { get; }
    Task UndoAsync();
    Task RedoAsync();
    void Push(IReversibleCommand command);
    void Clear();
}

[EngineService(typeof(ICommandHistoryService))]
public class CommandHistoryService : EngineService, ICommandHistoryService
{
    private readonly Stack<IReversibleCommand> _undoStack = new();
    private readonly Stack<IReversibleCommand> _redoStack = new();
    private readonly ILogger<CommandHistoryService> _logger;
    private readonly System.Threading.Lock _lock = new();

    public CommandHistoryService(ILogger<CommandHistoryService> logger)
    {
        _logger = logger;
    }

    public bool CanUndo => _undoStack.Count > 0;
    public bool CanRedo => _redoStack.Count > 0;

    public void Push(IReversibleCommand command)
    {
        using (_lock.EnterScope())
        {
            _undoStack.Push(command);
            _redoStack.Clear(); // New command breaks redo chain
        }
    }

    public async Task UndoAsync()
    {
        IReversibleCommand? command = null;
        using (_lock.EnterScope())
        {
            if (_undoStack.Count > 0)
            {
                command = _undoStack.Pop();
            }
        }

        if (command != null)
        {
            try
            {
                await command.UndoAsync();
                using (_lock.EnterScope()) _redoStack.Push(command);
                _logger.LogDebug("Undone command: {CommandName}", command.Name);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to undo command: {CommandName}", command.Name);
                Clear(); // Inconsistent state, clear history
            }
        }
    }

    public async Task RedoAsync()
    {
        IReversibleCommand? command = null;
        using (_lock.EnterScope())
        {
            if (_redoStack.Count > 0)
            {
                command = _redoStack.Pop();
            }
        }

        if (command != null)
        {
            try
            {
                await command.ExecuteAsync();
                using (_lock.EnterScope()) _undoStack.Push(command);
                _logger.LogDebug("Redone command: {CommandName}", command.Name);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to redo command: {CommandName}", command.Name);
                Clear();
            }
        }
    }

    public void Clear()
    {
        using (_lock.EnterScope())
        {
            _undoStack.Clear();
            _redoStack.Clear();
        }
    }
}
