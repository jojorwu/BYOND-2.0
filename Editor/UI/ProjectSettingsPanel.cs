using ImGuiNET;
using Core;

namespace Editor.UI
{
    public class ProjectSettingsPanel
    {
        private readonly Project _project;

        public ProjectSettingsPanel(Project project)
        {
            _project = project;
        }

        public void Draw()
        {
            ImGui.Begin("Project Settings");
            var mainMap = _project.Settings.MainMap ?? "";
            if (ImGui.InputText("Main Map", ref mainMap, 256))
            {
                _project.Settings.MainMap = mainMap;
                _project.SaveSettings();
            }
            ImGui.End();
        }
    }
}
