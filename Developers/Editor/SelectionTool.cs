using Shared;
using Shared.Models;
using Shared.Services;
using Shared.Interfaces;
using Shared.Enums;
using Shared.Operations;
using Robust.Shared.Maths;
using Editor;
using System.Linq;

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
                selectionManager.Select(turf.Contents.First());
            }
            else
            {
                selectionManager.Deselect();
            }
        }

        public void OnMouseUp(EditorContext context, GameState gameState, SelectionManager selectionManager, Vector2i mousePosition) { }
        public void OnMouseMove(EditorContext context, GameState gameState, SelectionManager selectionManager, Vector2i mousePosition) { }
        public void Draw(EditorContext context, GameState gameState, SelectionManager selectionManager) { }
    }
}
