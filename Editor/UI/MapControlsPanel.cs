using ImGuiNET;
using Core;

namespace Editor.UI
{
    public class MapControlsPanel
    {
        private readonly Editor _editor;
        private readonly GameState _gameState;

        public MapControlsPanel(Editor editor, GameState gameState)
        {
            _editor = editor;
            _gameState = gameState;
        }

        public void Draw()
        {
            ImGui.Begin("Map Controls");
            ImGui.Text($"Current Z-Level: {_editor.CurrentZLevel}");
            if (ImGui.Button("-"))
            {
                _editor.ChangeZLevel(-1);
            }
            ImGui.SameLine();
            if (ImGui.Button("+"))
            {
                _editor.ChangeZLevel(1);
            }
            ImGui.End();
        }
    }
}
