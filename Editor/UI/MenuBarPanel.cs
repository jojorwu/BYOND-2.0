
using ImGuiNET;

namespace Editor.UI
{
    public enum MenuBarAction
    {
        None,
        SaveMap,
        LoadMap,
        GoToMainMenu,
        OpenSettings
    }

    public class MenuBarPanel
    {
        public MenuBarAction Draw()
        {
            var action = MenuBarAction.None;

            if (ImGui.BeginMainMenuBar())
            {
                if (ImGui.BeginMenu("File"))
                {
                    if (ImGui.MenuItem("Save Map"))
                    {
                        action = MenuBarAction.SaveMap;
                    }
                    if (ImGui.MenuItem("Load Map"))
                    {
                        action = MenuBarAction.LoadMap;
                    }
                    if (ImGui.MenuItem("Back to Main Menu"))
                    {
                        action = MenuBarAction.GoToMainMenu;
                    }
                    ImGui.EndMenu();
                }
                if (ImGui.BeginMenu("Edit"))
                {
                    if (ImGui.MenuItem("Engine Settings"))
                    {
                        action = MenuBarAction.OpenSettings;
                    }
                    ImGui.EndMenu();
                }
                ImGui.EndMainMenuBar();
            }

            return action;
        }
    }
}
