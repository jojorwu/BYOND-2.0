using System.Collections.Generic;
using Silk.NET.OpenGL;
using Silk.NET.Windowing;
using ImGuiNET;

namespace Editor;

public interface IEditorUIService
{
    void Initialize(GL gl, IWindow window);
    void Render(float dt);
    void RegisterPanel(IEditorPanel panel);
}

public interface IEditorPanel
{
    string Title { get; }
    void Render();
}

/// <summary>
/// Orchestrates the ImGui-based Editor UI with docking support.
/// </summary>
public class EditorUIService : IEditorUIService
{
    private readonly List<IEditorPanel> _panels = new();

    public EditorUIService(
        MenuBarPanel menuBar,
        HierarchyPanel hierarchy,
        InspectorPanel inspector,
        AssetBrowserPanel assetBrowser,
        ViewportPanel viewport,
        IToolManager toolManager,
        SelectionTool selectionTool,
            PaintTool paintTool,
            EraserTool eraserTool)
    {
        _panels.Add(menuBar);
        _panels.Add(hierarchy);
        _panels.Add(inspector);
        _panels.Add(assetBrowser);
        _panels.Add(viewport);

        _toolManager = toolManager;
        _toolManager.RegisterTool(selectionTool);
        _toolManager.RegisterTool(paintTool);
        _toolManager.RegisterTool(eraserTool);
        _toolManager.ActiveTool = selectionTool;
    }

    public void Initialize(GL gl, IWindow window)
    {
        var io = ImGui.GetIO();
        io.ConfigFlags |= ImGuiConfigFlags.DockingEnable;
        ApplyDarkTheme();
    }

    private void ApplyDarkTheme()
    {
        var style = ImGui.GetStyle();
        var colors = style.Colors;

        colors[(int)ImGuiCol.Text]                   = new System.Numerics.Vector4(1.00f, 1.00f, 1.00f, 1.00f);
        colors[(int)ImGuiCol.WindowBg]               = new System.Numerics.Vector4(0.06f, 0.06f, 0.06f, 0.94f);
        colors[(int)ImGuiCol.ChildBg]                = new System.Numerics.Vector4(0.00f, 0.00f, 0.00f, 0.00f);
        colors[(int)ImGuiCol.Header]                 = new System.Numerics.Vector4(0.20f, 0.20f, 0.20f, 1.00f);
        colors[(int)ImGuiCol.HeaderHovered]          = new System.Numerics.Vector4(0.25f, 0.25f, 0.25f, 1.00f);
        colors[(int)ImGuiCol.HeaderActive]           = new System.Numerics.Vector4(0.30f, 0.30f, 0.30f, 1.00f);
        colors[(int)ImGuiCol.Button]                 = new System.Numerics.Vector4(0.20f, 0.20f, 0.20f, 1.00f);
        colors[(int)ImGuiCol.ButtonHovered]          = new System.Numerics.Vector4(0.30f, 0.30f, 0.30f, 1.00f);
        colors[(int)ImGuiCol.ButtonActive]           = new System.Numerics.Vector4(0.10f, 0.10f, 0.10f, 1.00f);
        colors[(int)ImGuiCol.FrameBg]                = new System.Numerics.Vector4(0.15f, 0.15f, 0.15f, 1.00f);
        colors[(int)ImGuiCol.TitleBg]                = new System.Numerics.Vector4(0.04f, 0.04f, 0.04f, 1.00f);
        colors[(int)ImGuiCol.TitleBgActive]          = new System.Numerics.Vector4(0.16f, 0.16f, 0.16f, 1.00f);
    }

    public void RegisterPanel(IEditorPanel panel) => _panels.Add(panel);

    private readonly IToolManager _toolManager;

    public void Render(float dt)
    {
        ImGui.DockSpaceOverViewport();

        foreach (var panel in _panels)
        {
            if (ImGui.Begin(panel.Title))
            {
                panel.Render();
            }
            ImGui.End();
        }
    }
}
