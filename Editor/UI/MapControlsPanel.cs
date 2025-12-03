using ImGuiNET;

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
            ImGui.Text("Z-Level");
            ImGui.SameLine();
            int zLevel = _editorContext.CurrentZLevel;
            ImGui.SetNextItemWidth(100);
            if (ImGui.InputInt("##ZLevel", ref zLevel))
            {
                _editorContext.CurrentZLevel = zLevel;
            }
            ImGui.SameLine();
            if (ImGui.Button("Up"))
            {
                _editorContext.CurrentZLevel++;
            }
            ImGui.SameLine();
            if (ImGui.Button("Down"))
            {
                _editorContext.CurrentZLevel--;
            }
            ImGui.End();
        }
    }
}
