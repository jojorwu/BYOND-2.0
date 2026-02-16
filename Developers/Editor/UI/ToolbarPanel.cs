using ImGuiNET;
using Editor.History;

namespace Editor.UI
{
    public class ToolbarPanel
    {
        private readonly EditorContext _editorContext;
        private readonly ToolManager _toolManager;
        private readonly IProjectService _projectService;
        private readonly IRunService _runService;
        private readonly HistoryManager _historyManager;

        public ToolbarPanel(EditorContext editorContext, ToolManager toolManager, IProjectService projectService, IRunService runService, HistoryManager historyManager)
        {
            _editorContext = editorContext;
            _toolManager = toolManager;
            _projectService = projectService;
            _runService = runService;
            _historyManager = historyManager;
        }

        public void Draw()
        {
            ImGuiWindowFlags flags = ImGuiWindowFlags.NoTitleBar | ImGuiWindowFlags.NoResize | ImGuiWindowFlags.NoMove | ImGuiWindowFlags.NoSavedSettings | ImGuiWindowFlags.NoScrollbar | ImGuiWindowFlags.NoScrollWithMouse;

            ImGui.PushStyleVar(ImGuiStyleVar.WindowPadding, new System.Numerics.Vector2(4, 4));
            if (ImGui.Begin("Toolbar", flags))
            {
                if (ImGui.Button("Save"))
                {
                    _projectService.SaveProject();
                }
                if (ImGui.IsItemHovered()) ImGui.SetTooltip("Save the current project and all open scenes.");

                ImGui.SameLine();
                if (ImGui.Button("Run"))
                {
                    _runService.Run();
                }
                if (ImGui.IsItemHovered()) ImGui.SetTooltip("Build and run the current project.");

                ImGui.SameLine();
                ImGui.TextDisabled("|");
                ImGui.SameLine();

                if (ImGui.Button("Undo") && _historyManager.CanUndo)
                {
                    _historyManager.Undo();
                }
                if (ImGui.IsItemHovered()) ImGui.SetTooltip("Undo the last action (Ctrl+Z).");

                ImGui.SameLine();
                if (ImGui.Button("Redo") && _historyManager.CanRedo)
                {
                    _historyManager.Redo();
                }
                if (ImGui.IsItemHovered()) ImGui.SetTooltip("Redo the previously undone action (Ctrl+Y).");

                ImGui.SameLine();
                ImGui.TextDisabled("|");
                ImGui.SameLine();

                foreach (var tool in _toolManager.Tools)
                {
                    bool isActive = _toolManager.GetActiveTool() == tool;
                    if (ImGui.Selectable(tool.Name, isActive, ImGuiSelectableFlags.None, new System.Numerics.Vector2(60, 0)))
                    {
                        _toolManager.SetActiveTool(tool, _editorContext);
                    }
                    if (ImGui.IsItemHovered()) ImGui.SetTooltip($"Select the {tool.Name} tool.");
                    ImGui.SameLine();
                }

                ImGui.End();
            }
            ImGui.PopStyleVar();
        }
    }
}
