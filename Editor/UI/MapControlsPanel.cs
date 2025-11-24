using Core;
using ImGuiNET;
using System;

namespace Editor.UI
{
    public class MapControlsPanel
    {
        private readonly EditorContext _editorContext;

        public MapControlsPanel(EditorContext editorContext)
        {
            _editorContext = editorContext;
        }

        public void Draw()
        {
            ImGui.Begin("Map Controls");
            ImGui.Text($"Current Z-Level: {_editorContext.CurrentZLevel}");
            if (ImGui.Button("+"))
            {
                _editorContext.CurrentZLevel++;
            }
            ImGui.SameLine();
            if (ImGui.Button("-") && _editorContext.CurrentZLevel > 0)
            {
                _editorContext.CurrentZLevel--;
            }
            ImGui.End();
        }
    }
}
