using ImGuiNET;

namespace Editor.UI
{
    public class ToolbarPanel
    {
        private readonly EditorContext _editorContext;
        private readonly ToolManager _toolManager;
        private readonly IProjectService _projectService;
        private readonly IRunService _runService;

        public ToolbarPanel(EditorContext editorContext, ToolManager toolManager, IProjectService projectService, IRunService runService)
        {
            _editorContext = editorContext;
            _toolManager = toolManager;
            _projectService = projectService;
            _runService = runService;
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
