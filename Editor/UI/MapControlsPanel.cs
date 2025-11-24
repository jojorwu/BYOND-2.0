using ImGuiNET;
using System;

namespace Editor.UI
{
    public class MapControlsPanel
    {
        private readonly Editor _editor;

        public MapControlsPanel(Editor editor)
        {
            _editor = editor;
        }

        public void Draw()
        {
            ImGui.Begin("Map Controls");
            ImGui.Text($"Current Z-Level: {_editor.CurrentZLevel}");
            if (ImGui.Button("Up"))
            {
                _editor.ChangeZLevel(1);
            }
            ImGui.SameLine();
            if (ImGui.Button("Down"))
            {
                _editor.ChangeZLevel(-1);
            }
            ImGui.End();
        }
    }
}
