using System.Collections.Generic;
using Shared;
using Shared.Interfaces;

namespace Editor;

/// <summary>
/// Defines a command that can be executed and reverted by the Editor's history system.
/// </summary>
public interface IEditorCommand
{
    string Name { get; }
    void Execute();
    void Undo();
}

/// <summary>
/// Manages the history of executed commands to provide Undo/Redo functionality.
/// </summary>
public class CommandHistory
{
    private readonly LinkedList<IEditorCommand> _undoStack = new();
    private readonly LinkedList<IEditorCommand> _redoStack = new();
    private const int MaxHistory = 100;

    public void Execute(IEditorCommand command)
    {
        command.Execute();
        _undoStack.AddLast(command);
        _redoStack.Clear();

        if (_undoStack.Count > MaxHistory)
        {
            _undoStack.RemoveFirst();
        }
    }

    public void Undo()
    {
        if (_undoStack.Last != null)
        {
            var command = _undoStack.Last.Value;
            _undoStack.RemoveLast();
            command.Undo();
            _redoStack.AddLast(command);
        }
    }

    public void Redo()
    {
        if (_redoStack.Last != null)
        {
            var command = _redoStack.Last.Value;
            _redoStack.RemoveLast();
            command.Execute();
            _undoStack.AddLast(command);
        }
    }

    public bool CanUndo => _undoStack.Count > 0;
    public bool CanRedo => _redoStack.Count > 0;
}

public class PlaceObjectCommand : IEditorCommand
{
    private readonly IGameState _gameState;
    private readonly ObjectType _type;
    private readonly long _x, _y, _z;
    private GameObject? _createdObject;

    public string Name => $"Place {_type.Name}";

    public PlaceObjectCommand(IGameState gameState, ObjectType type, long x, long y, long z)
    {
        _gameState = gameState;
        _type = type;
        _x = x;
        _y = y;
        _z = z;
    }

    public void Execute()
    {
        if (_createdObject == null)
        {
            _createdObject = new GameObject(_type, _x, _y, _z);
            _gameState.AddGameObject(_createdObject);
        }
        else
        {
            _gameState.AddGameObject(_createdObject);
        }
    }

    public void Undo()
    {
        if (_createdObject != null)
        {
            _gameState.RemoveGameObject(_createdObject);
        }
    }
}

public class DeleteObjectCommand : IEditorCommand
{
    private readonly IGameState _gameState;
    private readonly GameObject _target;
    private readonly long _x, _y, _z;

    public string Name => $"Delete {_target.ObjectType?.Name ?? "Object"}";

    public DeleteObjectCommand(IGameState gameState, GameObject target)
    {
        _gameState = gameState;
        _target = target;
        _x = target.X;
        _y = target.Y;
        _z = target.Z;
    }

    public void Execute()
    {
        _gameState.RemoveGameObject(_target);
    }

    public void Undo()
    {
        _gameState.AddGameObject(_target);
    }
}
