using ImGuiNET;
using Shared;
using Shared.Interfaces;
using Shared.Enums;

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

public class TypePalettePanel : IEditorPanel
{
    private readonly IObjectTypeManager _typeManager;
    private readonly EditorState _state;
    private string _filter = "";

    public TypePalettePanel(IObjectTypeManager typeManager, EditorState state)
    {
        _typeManager = typeManager;
        _state = state;
    }

    public string Title => "Type Palette";

    public void Render()
    {
        ImGui.InputText("Filter", ref _filter, 64);
        ImGui.Separator();

        ImGui.BeginChild("TypeList");
        foreach (var type in _typeManager.GetAllObjectTypes())
        {
            if (!string.IsNullOrEmpty(_filter) && !type.Name.Contains(_filter, StringComparison.OrdinalIgnoreCase))
                continue;

            if (ImGui.Selectable(type.Name, _state.SelectedTypeName == type.Name))
            {
                _state.SelectedTypeName = type.Name;
            }
        }
        ImGui.EndChild();
    }
}

public class ToolbarPanel : IEditorPanel
{
    private readonly IToolManager _toolManager;
    public ToolbarPanel(IToolManager toolManager) => _toolManager = toolManager;
    public string Title => "Tools";
    public void Render()
    {
        var manager = (ToolManager)_toolManager;
        foreach (var tool in manager.Tools)
        {
            bool isActive = _toolManager.ActiveTool == tool;
            if (isActive) ImGui.PushStyleColor(ImGuiCol.Button, new System.Numerics.Vector4(0.3f, 0.5f, 0.8f, 1.0f));

            if (ImGui.Button(tool.Name))
            {
                _toolManager.ActiveTool = tool;
                tool.OnSelected();
            }

            if (isActive) ImGui.PopStyleColor();
            ImGui.SameLine();
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

        // Standard Transform properties
        if (ImGui.CollapsingHeader("Transform", ImGuiTreeNodeFlags.DefaultOpen))
        {
            int x = (int)obj.X;
            int y = (int)obj.Y;
            int z = (int)obj.Z;

            if (ImGui.InputInt("X", ref x)) obj.X = x;
            if (ImGui.InputInt("Y", ref y)) obj.Y = y;
            if (ImGui.InputInt("Z", ref z)) obj.Z = z;
        }

        // Dynamic Variables
        if (obj.ObjectType != null && ImGui.CollapsingHeader("Variables", ImGuiTreeNodeFlags.DefaultOpen))
        {
            var type = obj.ObjectType;
            for (int i = 0; i < type.VariableNames.Count; i++)
            {
                var name = type.VariableNames[i];
                var val = obj.GetVariableDirect(i);

                if (val.Type == DreamValueType.Float || val.Type == DreamValueType.Integer)
                {
                    float f = val.AsFloat();
                    if (ImGui.InputFloat(name, ref f))
                    {
                        obj.SetVariableDirect(i, new DreamValue(f));
                    }
                }
                else if (val.Type == DreamValueType.String)
                {
                    string s = val.StringValue;
                    if (ImGui.InputText(name, ref s, 256))
                    {
                        obj.SetVariableDirect(i, new DreamValue(s));
                    }
                }
                else
                {
                    ImGui.LabelText(name, val.ToString());
                }
            }
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
