using Shared;
using Robust.Shared.Maths;
using Editor;
using System.Linq;
using ImGuiNET;

namespace Editor
{
    public class SelectionTool : ITool
    {
        public string Name => "Select";

        public void OnSelected(EditorContext context) { }
        public void OnDeselected(EditorContext context) { }

        public void OnMouseDown(EditorContext context, GameState gameState, SelectionManager selectionManager, Vector2i mousePosition)
        {
            if (gameState.Map == null) return;

            var tileX = mousePosition.X / EditorConstants.TileSize;
            var tileY = mousePosition.Y / EditorConstants.TileSize;

            var turf = gameState.Map.GetTurf(tileX, tileY, context.CurrentZLevel);
            if (turf != null && turf.Contents.Any())
            {
                var obj = turf.Contents.First();
                if (ImGui.GetIO().KeyCtrl)
                {
                    selectionManager.ToggleSelection(obj);
                }
                else
                {
                    selectionManager.Select(obj);
                }
            }
            else
            {
                if (!ImGui.GetIO().KeyCtrl)
                    selectionManager.Deselect();
            }
        }

        public void OnMouseUp(EditorContext context, GameState gameState, SelectionManager selectionManager, Vector2i mousePosition) { }
        public void OnMouseMove(EditorContext context, GameState gameState, SelectionManager selectionManager, Vector2i mousePosition) { }
        public void Draw(EditorContext context, GameState gameState, SelectionManager selectionManager) { }
    }
}
