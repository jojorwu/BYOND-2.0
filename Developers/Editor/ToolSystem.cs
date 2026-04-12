using System;
using System.Collections.Generic;
using Shared;
using Shared.Interfaces;

namespace Editor;

public interface ITool
{
    string Name { get; }
    void OnSelected();
    void OnMouseDown(float x, float y);
    void OnMouseMove(float x, float y);
    void OnMouseUp(float x, float y);
}

public interface IToolManager
{
    ITool? ActiveTool { get; set; }
    void RegisterTool(ITool tool);
    IReadOnlyList<ITool> Tools { get; }
}

/// <summary>
/// Manages Editor tools and user interaction modes.
/// </summary>
public class ToolManager : IToolManager
{
    private readonly List<ITool> _tools = new();
    public ITool? ActiveTool { get; set; }
    public IReadOnlyList<ITool> Tools => _tools;

    public void RegisterTool(ITool tool) => _tools.Add(tool);
}

public class SelectionTool : ITool
{
    private readonly EditorContext _context;
    private readonly EditorState _state;
    private readonly IGameState _gameState;
    private bool _isDragging;
    private long _dragStartX, _dragStartY;
    private GameObject? _draggingObject;

    public SelectionTool(EditorContext context, EditorState state, IGameState gameState)
    {
        _context = context;
        _state = state;
        _gameState = gameState;
    }

    public string Name => "Select";
    public void OnSelected() { }

    public void OnMouseDown(float x, float y)
    {
        _state.SelectedEntityId = -1;
        _draggingObject = null;
        _isDragging = false;

        foreach (var obj in _gameState.GetAllGameObjects())
        {
            if (Math.Abs(obj.X - x) < 16 && Math.Abs(obj.Y - y) < 16)
            {
                _state.SelectedEntityId = obj.Id;
                _draggingObject = (GameObject)obj;
                _dragStartX = _draggingObject.X;
                _dragStartY = _draggingObject.Y;
                _isDragging = true;
                break;
            }
        }
    }

    public void OnMouseMove(float x, float y)
    {
        if (_isDragging && _draggingObject != null)
        {
            long targetX = (long)x;
            long targetY = (long)y;

            if (_state.SnapToGrid)
            {
                targetX = (long)Math.Round(x / _state.GridSize) * _state.GridSize;
                targetY = (long)Math.Round(y / _state.GridSize) * _state.GridSize;
            }

            _draggingObject.SetPosition(targetX, targetY, _draggingObject.Z);
        }
    }

    public void OnMouseUp(float x, float y)
    {
        if (_isDragging && _draggingObject != null)
        {
            if (_draggingObject.X != _dragStartX || _draggingObject.Y != _dragStartY)
            {
                var finalX = _draggingObject.X;
                var finalY = _draggingObject.Y;
                _draggingObject.SetPosition(_dragStartX, _dragStartY, _draggingObject.Z);

                _context.History.Execute(new MoveObjectCommand(_draggingObject, finalX, finalY, _draggingObject.Z));
            }
        }
        _isDragging = false;
        _draggingObject = null;
    }
}

public class PaintTool : ITool
{
    private readonly EditorContext _context;
    private readonly EditorState _state;
    private readonly IGameState _gameState;
    private readonly IObjectTypeManager _typeManager;

    public PaintTool(EditorContext context, EditorState state, IGameState gameState, IObjectTypeManager typeManager)
    {
        _context = context;
        _state = state;
        _gameState = gameState;
        _typeManager = typeManager;
    }

    public string Name => "Paint";
    public void OnSelected() { }
    public void OnMouseDown(float x, float y)
    {
        if (string.IsNullOrEmpty(_state.SelectedTypeName)) return;

        long targetX = (long)x;
        long targetY = (long)y;

        if (_state.SnapToGrid)
        {
            targetX = (long)Math.Round(x / _state.GridSize) * _state.GridSize;
            targetY = (long)Math.Round(y / _state.GridSize) * _state.GridSize;
        }

        var type = _typeManager.GetObjectType(_state.SelectedTypeName);
        if (type != null)
        {
            var command = new PlaceObjectCommand(_gameState, type, targetX, targetY, 0);
            _context.History.Execute(command);
        }
    }
    public void OnMouseMove(float x, float y) { }
    public void OnMouseUp(float x, float y) { }
}

public class EraserTool : ITool
{
    private readonly EditorContext _context;
    private readonly EditorState _state;
    private readonly IGameState _gameState;

    public EraserTool(EditorContext context, EditorState state, IGameState gameState)
    {
        _context = context;
        _state = state;
        _gameState = gameState;
    }

    public string Name => "Eraser";
    public void OnSelected() { }
    public void OnMouseDown(float x, float y)
    {
        long targetX = (long)x;
        long targetY = (long)y;

        if (_state.SnapToGrid)
        {
            targetX = (long)Math.Round(x / _state.GridSize) * _state.GridSize;
            targetY = (long)Math.Round(y / _state.GridSize) * _state.GridSize;
        }

        foreach (var obj in _gameState.GetAllGameObjects())
        {
            if (obj.X == targetX && obj.Y == targetY)
            {
                var command = new DeleteObjectCommand(_gameState, (GameObject)obj);
                _context.History.Execute(command);
                break;
            }
        }
    }
    public void OnMouseMove(float x, float y) { }
    public void OnMouseUp(float x, float y) { }
}
