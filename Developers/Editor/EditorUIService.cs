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
        ConsolePanel console,
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
        _panels.Add(console);

        toolManager.RegisterTool(selectionTool);
        toolManager.RegisterTool(paintTool);
        toolManager.RegisterTool(eraserTool);
        toolManager.ActiveTool = selectionTool;
    }

    public void Initialize(GL gl, IWindow window)
    {
        var io = ImGui.GetIO();
        io.ConfigFlags |= ImGuiConfigFlags.DockingEnable;
    }

    public void RegisterPanel(IEditorPanel panel) => _panels.Add(panel);

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
