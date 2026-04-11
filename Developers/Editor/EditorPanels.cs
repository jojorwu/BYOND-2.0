using ImGuiNET;
using System.Numerics;
using Shared;
using Shared.Interfaces;

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
                if (ImGui.MenuItem("Undo", "Ctrl+Z")) { /* TODO */ }
                if (ImGui.MenuItem("Redo", "Ctrl+Y")) { /* TODO */ }
                ImGui.EndMenu();
            }

            ImGui.EndMainMenuBar();
        }
    }
}

public class HierarchyPanel : IEditorPanel
{
    private readonly LocalizationService _loc;
    public HierarchyPanel(LocalizationService loc) => _loc = loc;
    public string Title => "Hierarchy";
    public void Render()
    {
        ImGui.Text(_loc.GetString("Panel.Hierarchy.Title"));
        ImGui.Separator();
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

    public ViewportPanel(EditorRenderer renderer, LocalizationService loc)
    {
        _renderer = renderer;
        _loc = loc;
    }

    public string Title => "Viewport";

    public void Render()
    {
        var size = ImGui.GetContentRegionAvail();
        if (size.X > 0 && size.Y > 0)
        {
            _renderer.Resize((int)size.X, (int)size.Y);
            ImGui.Image((IntPtr)_renderer.ViewportTexture, size, new System.Numerics.Vector2(0, 1), new System.Numerics.Vector2(1, 0));
        }
    }
}
