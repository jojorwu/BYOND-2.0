using ImGuiNET;
using System.Numerics;
using Shared;
using Shared.Interfaces;
using System.Collections.Generic;

namespace Editor;

/// <summary>
/// Core menu bar for the Editor application.
/// </summary>
public class MenuBarPanel : IEditorPanel
{
    private readonly EditorContext _context;
    private readonly LocalizationService _loc;

    public MenuBarPanel(EditorContext context, LocalizationService loc)
    {
        _context = context;
        _loc = loc;
    }

    public string Title => "MenuBar";

    public void Render()
    {
        if (ImGui.BeginMainMenuBar())
        {
            if (ImGui.BeginMenu(_loc.GetString("Menu.File")))
            {
                if (ImGui.MenuItem("New Project")) { /* TODO */ }
                if (ImGui.MenuItem("Open Project")) { /* TODO */ }
                ImGui.Separator();
                if (ImGui.MenuItem("Exit")) { /* TODO */ }
                ImGui.EndMenu();
            }

            if (ImGui.BeginMenu(_loc.GetString("Menu.Edit")))
            {
                if (ImGui.MenuItem("Undo", "Ctrl+Z", false, _context.History.CanUndo)) { _context.History.Undo(); }
                if (ImGui.MenuItem("Redo", "Ctrl+Y", false, _context.History.CanRedo)) { _context.History.Redo(); }
                ImGui.EndMenu();
            }

            if (ImGui.BeginMenu("View"))
            {
                var snap = _context.State.SnapToGrid;
                if (ImGui.MenuItem("Snap to Grid", "", ref snap)) _context.State.SnapToGrid = snap;

                int gridSize = _context.State.GridSize;
                if (ImGui.InputInt("Grid Size", ref gridSize)) _context.State.GridSize = Math.Max(8, gridSize);

                ImGui.EndMenu();
            }

            ImGui.EndMainMenuBar();
        }
    }
}

public class HierarchyPanel : IEditorPanel
{
    private readonly LocalizationService _loc;
    private readonly EditorState _state;
    private readonly IGameState _gameState;

    public HierarchyPanel(LocalizationService loc, EditorState state, IGameState gameState)
    {
        _loc = loc;
        _state = state;
        _gameState = gameState;
    }

    public string Title => "Hierarchy";

    public void Render()
    {
        ImGui.Text(_loc.GetString("Panel.Hierarchy.Title"));
        ImGui.Separator();

        ImGui.BeginChild("HierarchyList");
        foreach (var obj in _gameState.GameObjects.Values)
        {
            bool isSelected = _state.SelectedEntityId == obj.Id;
            if (ImGui.Selectable($"{obj.Id}: {obj.TypeName}##{obj.Id}", isSelected))
            {
                _state.SelectedEntityId = obj.Id;
            }

            if (isSelected)
            {
                ImGui.SetItemDefaultFocus();
            }
        }
        ImGui.EndChild();
    }
}

public class InspectorPanel : IEditorPanel
{
    private readonly LocalizationService _loc;
    private readonly EditorState _state;
    private readonly IGameState _gameState;

    public InspectorPanel(LocalizationService loc, EditorState state, IGameState gameState)
    {
        _loc = loc;
        _state = state;
        _gameState = gameState;
    }

    public string Title => "Inspector";

    public void Render()
    {
        ImGui.Text(_loc.GetString("Panel.Inspector.Title"));
        ImGui.Separator();

        if (_state.SelectedEntityId == -1)
        {
            ImGui.Text("No entity selected.");
            return;
        }

        if (!_gameState.GameObjects.TryGetValue(_state.SelectedEntityId, out var obj))
        {
            ImGui.Text("Selected entity not found.");
            return;
        }

        ImGui.Text($"ID: {obj.Id}");
        ImGui.Text($"Type: {obj.TypeName}");
        ImGui.Separator();

        // Transform
        if (ImGui.CollapsingHeader("Transform", ImGuiTreeNodeFlags.DefaultOpen))
        {
            int x = (int)obj.X;
            int y = (int)obj.Y;
            int z = (int)obj.Z;

            if (ImGui.InputInt("X", ref x)) obj.X = x;
            if (ImGui.InputInt("Y", ref y)) obj.Y = y;
            if (ImGui.InputInt("Z", ref z)) obj.Z = z;
        }

        // Visuals
        if (ImGui.CollapsingHeader("Visuals", ImGuiTreeNodeFlags.DefaultOpen))
        {
            string icon = obj.Icon;
            string state = obj.IconState;
            int dir = obj.Dir;

            if (ImGui.InputText("Icon", ref icon, 256)) obj.Icon = icon;
            if (ImGui.InputText("Icon State", ref state, 256)) obj.IconState = state;
            if (ImGui.InputInt("Direction", ref dir)) obj.Dir = dir;
        }
    }
}

public class AssetBrowserPanel : IEditorPanel
{
    private readonly LocalizationService _loc;
    private readonly EditorState _state;
    private string _currentDirectory = "";

    public AssetBrowserPanel(LocalizationService loc, EditorState state)
    {
        _loc = loc;
        _state = state;
    }

    public string Title => "Asset Browser";

    public void Render()
    {
        ImGui.Text(_loc.GetString("Panel.AssetBrowser.Title"));
        ImGui.Separator();

        if (string.IsNullOrEmpty(_state.CurrentProjectPath))
        {
            ImGui.Text("No project loaded.");
            return;
        }

        if (string.IsNullOrEmpty(_currentDirectory))
            _currentDirectory = _state.CurrentProjectPath;

        ImGui.Text($"Path: {_currentDirectory}");
        if (ImGui.Button("..") && _currentDirectory != _state.CurrentProjectPath)
        {
            var parent = System.IO.Path.GetDirectoryName(_currentDirectory);
            if (!string.IsNullOrEmpty(parent)) _currentDirectory = parent;
        }

        ImGui.BeginChild("FileTree");
        try
        {
            if (System.IO.Directory.Exists(_currentDirectory))
            {
                foreach (var dir in System.IO.Directory.GetDirectories(_currentDirectory))
                {
                    var name = System.IO.Path.GetFileName(dir);
                    if (ImGui.Selectable($"[DIR] {name}", false, ImGuiSelectableFlags.AllowDoubleClick))
                    {
                        if (ImGui.IsMouseDoubleClicked(ImGuiMouseButton.Left))
                            _currentDirectory = dir;
                    }
                }

                foreach (var file in System.IO.Directory.GetFiles(_currentDirectory))
                {
                    var name = System.IO.Path.GetFileName(file);
                    if (ImGui.Selectable(name, false, ImGuiSelectableFlags.AllowDoubleClick))
                    {
                        // Handle file selection/open
                    }
                }
            }
        }
        catch (System.Exception e)
        {
            ImGui.TextColored(new Vector4(1, 0, 0, 1), $"Error: {e.Message}");
        }
        ImGui.EndChild();
    }
}

public class ViewportPanel : IEditorPanel
{
    private readonly EditorRenderer _renderer;
    private readonly IToolManager _toolManager;
    private readonly LocalizationService _loc;

    public ViewportPanel(EditorRenderer renderer, IToolManager toolManager, LocalizationService loc)
    {
        _renderer = renderer;
        _toolManager = toolManager;
        _loc = loc;
    }

    public string Title => "Viewport";

    public void Render()
    {
        var size = ImGui.GetContentRegionAvail();
        if (size.X > 0 && size.Y > 0)
        {
            _renderer.Resize((int)size.X, (int)size.Y);
            var cursorPos = ImGui.GetCursorScreenPos();
            ImGui.Image((IntPtr)_renderer.ViewportTexture, size, new System.Numerics.Vector2(0, 1), new System.Numerics.Vector2(1, 0));

            // Tool interaction logic
            if (ImGui.IsItemHovered())
            {
                var io = ImGui.GetIO();
                var mousePos = io.MousePos - cursorPos;

                // Convert screen space to world space
                var worldPos = ScreenToWorld(mousePos, size);

                if (ImGui.IsMouseClicked(ImGuiMouseButton.Left))
                {
                    _toolManager.ActiveTool?.OnMouseDown(worldPos.X, worldPos.Y);
                }

                _toolManager.ActiveTool?.OnMouseMove(worldPos.X, worldPos.Y);

                if (ImGui.IsMouseReleased(ImGuiMouseButton.Left))
                {
                    _toolManager.ActiveTool?.OnMouseUp(worldPos.X, worldPos.Y);
                }

                // Viewport Navigation logic (Pan/Zoom)
                if (ImGui.IsMouseDragging(ImGuiMouseButton.Middle) || ImGui.IsMouseDragging(ImGuiMouseButton.Right))
                {
                    var delta = io.MouseDelta;
                    _renderer.Camera.Position -= new System.Numerics.Vector2(delta.X, delta.Y) / _renderer.Camera.Zoom;
                }

                if (io.MouseWheel != 0)
                {
                    float zoomFactor = 1.1f;
                    if (io.MouseWheel > 0) _renderer.Camera.Zoom *= zoomFactor;
                    else _renderer.Camera.Zoom /= zoomFactor;
                    _renderer.Camera.Zoom = Math.Clamp(_renderer.Camera.Zoom, 0.01f, 10f);
                }
            }
        }
    }

    private System.Numerics.Vector2 ScreenToWorld(System.Numerics.Vector2 screenPos, System.Numerics.Vector2 viewportSize)
    {
        // Simple inverse of projection/view (orthographic)
        var centered = screenPos - viewportSize / 2;
        var scaled = centered / _renderer.Camera.Zoom;
        return scaled + _renderer.Camera.Position;
    }
}

public class ConsolePanel : IEditorPanel
{
    private readonly IDiagnosticBus _diagnosticBus;
    private readonly List<DiagnosticEvent> _logs = new();
    private readonly IDisposable _subscription;

    public ConsolePanel(IDiagnosticBus diagnosticBus)
    {
        _diagnosticBus = diagnosticBus;
        _subscription = _diagnosticBus.Subscribe(OnDiagnosticEvent);
    }

    public string Title => "Console";

    private void OnDiagnosticEvent(DiagnosticEvent e)
    {
        // We need to clone it because DiagnosticEvent might be reused (as hinted by internal Clear() in source)
        var clone = new DiagnosticEvent
        {
            Source = e.Source,
            Message = e.Message,
            Severity = e.Severity,
            Tags = e.Tags
        };
        foreach (var m in e.Metrics) clone.Add(m.Key, m.Value);

        lock (_logs)
        {
            _logs.Add(clone);
            if (_logs.Count > 1000) _logs.RemoveAt(0);
        }
    }

    public void Render()
    {
        if (ImGui.Button("Clear")) { lock (_logs) _logs.Clear(); }
        ImGui.Separator();

        ImGui.BeginChild("LogScroll");
        lock (_logs)
        {
            foreach (var log in _logs)
            {
                var color = log.Severity switch
                {
                    DiagnosticSeverity.Error => new Vector4(1, 0.4f, 0.4f, 1),
                    DiagnosticSeverity.Warning => new Vector4(1, 1, 0.4f, 1),
                    _ => new Vector4(1, 1, 1, 1)
                };
                ImGui.TextColored(color, $"[{log.Source}] {log.Message}");
            }
        }
        if (ImGui.GetScrollY() >= ImGui.GetScrollMaxY())
            ImGui.SetScrollHereY(1.0f);
        ImGui.EndChild();
    }
}
