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
}

/// <summary>
/// Manages Editor tools and user interaction modes.
/// </summary>
public class ToolManager : IToolManager
{
    private readonly List<ITool> _tools = new();
    public ITool? ActiveTool { get; set; }

    public void RegisterTool(ITool tool) => _tools.Add(tool);
}

public class SelectionTool : ITool
{
    private readonly EditorState _state;
    private readonly IGameState _gameState;

    public SelectionTool(EditorState state, IGameState gameState)
    {
        _state = state;
        _gameState = gameState;
    }

    public string Name => "Select";
    public void OnSelected() { }
    public void OnMouseDown(float x, float y)
    {
        // Simple picking
        _state.SelectedEntityId = -1;
        foreach (var obj in _gameState.GetAllGameObjects())
        {
            if (obj.X == (long)x && obj.Y == (long)y)
            {
                _state.SelectedEntityId = obj.Id;
                break;
            }
        }
    }
    public void OnMouseMove(float x, float y) { }
    public void OnMouseUp(float x, float y) { }
}

public class PaintTool : ITool
{
    private readonly EditorContext _context;
    private readonly IGameState _gameState;
    private readonly IObjectTypeManager _typeManager;

    public PaintTool(EditorContext context, IGameState gameState, IObjectTypeManager typeManager)
    {
        _context = context;
        _gameState = gameState;
        _typeManager = typeManager;
    }

    public string Name => "Paint";
    public void OnSelected() { }
    public void OnMouseDown(float x, float y)
    {
        // Simple placement for now. In real editor we'd use world coordinates from viewport.
        var type = _typeManager.GetObjectType("obj"); // Example type
        if (type != null)
        {
            var command = new PlaceObjectCommand(_gameState, type, (long)x, (long)y, 0);
            _context.History.Execute(command);
        }
    }
    public void OnMouseMove(float x, float y) { }
    public void OnMouseUp(float x, float y) { }
}

public class EraserTool : ITool
{
    private readonly EditorContext _context;
    private readonly IGameState _gameState;

    public EraserTool(EditorContext context, IGameState gameState)
    {
        _context = context;
        _gameState = gameState;
    }

    public string Name => "Eraser";
    public void OnSelected() { }
    public void OnMouseDown(float x, float y)
    {
        // Simple deletion.
        foreach (var obj in _gameState.GetAllGameObjects())
        {
            if (obj.X == (long)x && obj.Y == (long)y)
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
