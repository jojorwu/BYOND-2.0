using System;
using System.Collections.Generic;

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
    public SelectionTool(EditorState state) => _state = state;
    public string Name => "Select";
    public void OnSelected() { }
    public void OnMouseDown(float x, float y) { }
    public void OnMouseMove(float x, float y) { }
    public void OnMouseUp(float x, float y) { }
}

public class PaintTool : ITool
{
    private readonly EditorContext _context;
    public PaintTool(EditorContext context) => _context = context;
    public string Name => "Paint";
    public void OnSelected() { }
    public void OnMouseDown(float x, float y) { }
    public void OnMouseMove(float x, float y) { }
    public void OnMouseUp(float x, float y) { }
}
