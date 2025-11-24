using ImGuiNET;

namespace Editor.UI
{
    public class MainMenuPanel
    {
        public string? Draw()
        {
            string? projectToLoad = null;
            ImGui.Begin("Main Menu");
            if (ImGui.Button("Load Project"))
            {
                // In a real application, this would open a file dialog.
                // For now, we'll just hardcode a path.
                projectToLoad = "projects/default";
            }
            ImGui.End();
            return projectToLoad;
        }
    }
}
