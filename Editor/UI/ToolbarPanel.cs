using ImGuiNET;

namespace Editor.UI
{
    public class ToolbarPanel : IUiPanel
    {
        private readonly EditorContext _editorContext;
        private readonly ToolManager _toolManager;
        private readonly IProjectService _projectService;
        // TODO: Add BuildService/RunService

        public ToolbarPanel(EditorContext editorContext, ToolManager toolManager, IProjectService projectService)
        {
            _editorContext = editorContext;
            _toolManager = toolManager;
            _projectService = projectService;
        }

        public void Draw()
        {
            ImGuiWindowFlags flags = ImGuiWindowFlags.NoTitleBar | ImGuiWindowFlags.NoResize | ImGuiWindowFlags.NoMove | ImGuiWindowFlags.NoSavedSettings | ImGuiWindowFlags.NoScrollbar | ImGuiWindowFlags.NoScrollWithMouse;
            if (ImGui.Begin("Toolbar", flags))
            {
                if (ImGui.Button("Save"))
                {
                    _projectService.SaveProject();
                }
                ImGui.SameLine();
                if (ImGui.Button("Run"))
                {
                    // TODO: Call run logic
                }
                ImGui.SameLine();
                ImGui.Separator();
                ImGui.SameLine();

                foreach (var tool in _toolManager.Tools)
                {
                    if (ImGui.Selectable(tool.Name, _toolManager.GetActiveTool() == tool))
                    {
                        _toolManager.SetActiveTool(tool, _editorContext);
                    }
                    ImGui.SameLine();
                }

                ImGui.End();
            }
        }
    }
}
