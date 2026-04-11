using ImGuiNET;
using Shared;
using Shared.Interfaces;

namespace Editor;

/// <summary>
/// Core menu bar for the Editor application.
/// </summary>
public class MenuBarPanel : IEditorPanel
{
    private readonly IToolManager _toolManager;
    private readonly EditorContext _context;
    private readonly LocalizationService _loc;

    public MenuBarPanel(EditorContext context, LocalizationService loc, IToolManager toolManager)
    {
        _context = context;
        _loc = loc;
        _toolManager = toolManager;
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
                if (ImGui.MenuItem("Undo", "Ctrl+Z")) { /* TODO */ }
                if (ImGui.MenuItem("Redo", "Ctrl+Y")) { /* TODO */ }
                ImGui.EndMenu();
            }

            if (ImGui.BeginMenu("Tools"))
            {
                foreach (var tool in _toolManager.GetTools())
                {
                    if (ImGui.MenuItem(tool.Name, "", _toolManager.ActiveTool == tool))
                    {
                        _toolManager.ActiveTool = tool;
                    }
                }
                ImGui.EndMenu();
            }

            ImGui.EndMainMenuBar();
        }
    }
}

public class HierarchyPanel : IEditorPanel
{
    private readonly LocalizationService _loc;
    private readonly IToolManager _toolManager;
    private readonly IGameState _gameState;
    private readonly EditorState _state;

    public HierarchyPanel(LocalizationService loc, IGameState gameState, EditorState state, IToolManager toolManager)
    {
        _loc = loc;
        _gameState = gameState;
        _state = state;
        _toolManager = toolManager;
    }

    public string Title => "Hierarchy";

    public void Render()
    {
        ImGui.Text(_loc.GetString("Panel.Hierarchy.Title"));
        ImGui.Separator();

        if (ImGui.BeginChild("HierarchyList"))
        {
            foreach (var obj in _gameState.GameObjects.Values)
            {
                bool isSelected = _state.SelectedEntityId == obj.Id;
                if (ImGui.Selectable($"{obj.Id}: {obj.ObjectType?.Name ?? "Object"}", isSelected))
                {
                    _state.SelectedEntityId = obj.Id;
                }
            }
            ImGui.EndChild();
        }
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

        if (_state.SelectedEntityId != -1 && _gameState.GameObjects.TryGetValue(_state.SelectedEntityId, out var obj))
        {
            ImGui.Text($"ID: {obj.Id}");
            ImGui.Text($"Type: {obj.ObjectType?.Name ?? "Object"}");
            ImGui.Separator();

            if (ImGui.CollapsingHeader("Transform", ImGuiTreeNodeFlags.DefaultOpen))
            {
                var x = (float)obj.X;
                var y = (float)obj.Y;
                var z = (float)obj.Z;

                if (ImGui.DragFloat("X", ref x)) obj.X = (long)x;
                if (ImGui.DragFloat("Y", ref y)) obj.Y = (long)y;
                if (ImGui.DragFloat("Z", ref z)) obj.Z = (long)z;
            }

            if (ImGui.CollapsingHeader("Visuals", ImGuiTreeNodeFlags.DefaultOpen))
            {
                var icon = obj.Icon;
                if (ImGui.InputText("Icon", ref icon, 256)) obj.Icon = icon;

                var state = obj.IconState;
                if (ImGui.InputText("State", ref state, 256)) obj.IconState = state;

                var color = obj.Color;
                if (ImGui.InputText("Color", ref color, 7)) obj.Color = color;
            }
        }
        else
        {
            ImGui.Text("No entity selected.");
        }
    }
}

public class AssetBrowserPanel : IEditorPanel
{
    private readonly LocalizationService _loc;
    public AssetBrowserPanel(LocalizationService loc) => _loc = loc;
    public string Title => "Asset Browser";
    public void Render()
    {
        ImGui.Text(_loc.GetString("Panel.AssetBrowser.Title"));
        ImGui.Separator();
    }
}

public class ViewportPanel : IEditorPanel
{
    private readonly EditorRenderer _renderer;
    private readonly LocalizationService _loc;
    private readonly IToolManager _toolManager;

    public ViewportPanel(EditorRenderer renderer, LocalizationService loc, IToolManager toolManager)
    {
        _renderer = renderer;
        _loc = loc;
        _toolManager = toolManager;
    }

    public string Title => "Viewport";

    public void Render()
    {
        var size = ImGui.GetContentRegionAvail();
        if (size.X > 0 && size.Y > 0)
        {
            _renderer.Resize((int)size.X, (int)size.Y);
            var pos = ImGui.GetCursorScreenPos();
            ImGui.Image((IntPtr)_renderer.ViewportTexture, size, new System.Numerics.Vector2(0, 1), new System.Numerics.Vector2(1, 0));

            if (ImGui.IsItemClicked(ImGuiMouseButton.Left))
            {
                var mousePos = ImGui.GetMousePos() - pos;
                _toolManager.ActiveTool?.OnMouseDown(mousePos.X, mousePos.Y);
            }
        }
    }
}
