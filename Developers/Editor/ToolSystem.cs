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
    IEnumerable<ITool> GetTools();
}

/// <summary>
/// Manages Editor tools and user interaction modes.
/// </summary>
public class ToolManager : IToolManager
{
    private readonly List<ITool> _tools = new();
    public ITool? ActiveTool { get; set; }

    public void RegisterTool(ITool tool) => _tools.Add(tool);

    public IEnumerable<ITool> GetTools() => _tools;
}

public class SelectionTool : ITool
{
    private readonly EditorState _state;
    private readonly EditorRenderer _renderer;
    private readonly IGameState _gameState;

    public SelectionTool(EditorState state, EditorRenderer renderer, IGameState gameState)
    {
        _state = state;
        _renderer = renderer;
        _gameState = gameState;
    }

    public string Name => "Select";
    public void OnSelected() { }

    public void OnMouseDown(float x, float y)
    {
        // Simple 2D picking logic based on camera
        var worldPos = _renderer.Camera.Position + new System.Numerics.Vector2(x, y) / _renderer.Camera.Zoom;

        long? bestHit = null;
        foreach (var obj in _gameState.GameObjects.Values)
        {
            // Simple tile-based hitbox check
            if (worldPos.X >= obj.X * 32 && worldPos.X < (obj.X + 1) * 32 &&
                worldPos.Y >= obj.Y * 32 && worldPos.Y < (obj.Y + 1) * 32)
            {
                bestHit = obj.Id;
            }
        }

        if (bestHit.HasValue)
        {
            _state.SelectedEntityId = bestHit.Value;
        }
        else
        {
            _state.SelectedEntityId = -1;
        }
    }

    public void OnMouseMove(float x, float y) { }
    public void OnMouseUp(float x, float y) { }
}

public class PaintTool : ITool
{
    private readonly EditorContext _context;
    private readonly EditorRenderer _renderer;
    private readonly IObjectApi _objectApi;
    private readonly IObjectTypeManager _typeManager;

    public PaintTool(EditorContext context, EditorRenderer renderer, IObjectApi objectApi, IObjectTypeManager typeManager)
    {
        _context = context;
        _renderer = renderer;
        _objectApi = objectApi;
        _typeManager = typeManager;
    }

    public string Name => "Paint";
    public void OnSelected() { }

    public void OnMouseDown(float x, float y)
    {
        var worldPos = _renderer.Camera.Position + new System.Numerics.Vector2(x, y) / _renderer.Camera.Zoom;
        var tileX = (long)Math.Floor(worldPos.X / 32.0);
        var tileY = (long)Math.Floor(worldPos.Y / 32.0);

        // For now, hardcoded player type or similar for testing
        var type = _typeManager.GetObjectType("/obj");
        if (type != null)
        {
            _objectApi.CreateObject(type.Id, tileX, tileY, 0);
        }
    }

    public void OnMouseMove(float x, float y) { }
    public void OnMouseUp(float x, float y) { }
}

public class EraserTool : ITool
{
    private readonly EditorRenderer _renderer;
    private readonly IGameState _gameState;
    private readonly IObjectApi _objectApi;

    public EraserTool(EditorRenderer renderer, IGameState gameState, IObjectApi objectApi)
    {
        _renderer = renderer;
        _gameState = gameState;
        _objectApi = objectApi;
    }

    public string Name => "Eraser";
    public void OnSelected() { }

    public void OnMouseDown(float x, float y)
    {
        var worldPos = _renderer.Camera.Position + new System.Numerics.Vector2(x, y) / _renderer.Camera.Zoom;

        long? hitId = null;
        foreach (var obj in _gameState.GameObjects.Values)
        {
            if (worldPos.X >= obj.X * 32 && worldPos.X < (obj.X + 1) * 32 &&
                worldPos.Y >= obj.Y * 32 && worldPos.Y < (obj.Y + 1) * 32)
            {
                hitId = obj.Id;
                break;
            }
        }

        if (hitId.HasValue)
        {
            _objectApi.DestroyObject(hitId.Value);
        }
    }

    public void OnMouseMove(float x, float y) { }
    public void OnMouseUp(float x, float y) { }
}
