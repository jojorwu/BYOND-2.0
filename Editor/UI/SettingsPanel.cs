using ImGuiNET;

namespace Editor.UI
{
    public class SettingsPanel
    {
        public void Draw()
        {
            ImGui.Begin("Settings");
            ImGui.Text("Settings will go here.");
            ImGui.End();
        }
    }
}
