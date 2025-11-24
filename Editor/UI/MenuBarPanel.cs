using ImGuiNET;
using System;

namespace Editor.UI
{
    public class MenuBarPanel
    {
        public Action? OnSaveMap;
        public Action? OnLoadMap;

        public void Draw()
        {
            if (ImGui.BeginMainMenuBar())
            {
                if (ImGui.BeginMenu("File"))
                {
                    if (ImGui.MenuItem("Save Map"))
                    {
                        OnSaveMap?.Invoke();
                    }
                    if (ImGui.MenuItem("Load Map"))
                    {
                        OnLoadMap?.Invoke();
                    }
                    ImGui.EndMenu();
                }
                ImGui.EndMainMenuBar();
            }
        }
    }
}
