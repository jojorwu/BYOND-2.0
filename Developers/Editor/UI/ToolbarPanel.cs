using Shared.Models;
using Shared.Interfaces;
using Shared.Enums;
using Shared.Operations;
using Shared.Services;
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
            if (ImGui.Begin("Toolbar", flags))
            {
                if (ImGui.Button("Save"))
                {
                    _projectService.SaveProject();
                }
                ImGui.SameLine();
                if (ImGui.Button("Run"))
                {
                    _runService.Run();
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
