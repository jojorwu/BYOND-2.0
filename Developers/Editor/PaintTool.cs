using Shared;
using Shared.Models;
using Shared.Services;
using Shared.Interfaces;
using Shared.Enums;
using Shared.Operations;
using Robust.Shared.Maths;

namespace Editor
{
    public class PaintTool : ITool
    {
        public string Name => "Paint";

        public void OnSelected(EditorContext context) { }
        public void OnDeselected(EditorContext context) { }

        public void OnMouseDown(EditorContext context, GameState gameState, SelectionManager selectionManager, Vector2i mousePosition)
        {
            if (gameState.Map == null || context.SelectedObjectType == null) return;

            var tileX = mousePosition.X / EditorConstants.TileSize;
            var tileY = mousePosition.Y / EditorConstants.TileSize;

            var newObject = new GameObject(context.SelectedObjectType);
            gameState.Map.GetTurf(tileX, tileY, context.CurrentZLevel)?.AddContent(newObject);
        }

        public void OnMouseUp(EditorContext context, GameState gameState, SelectionManager selectionManager, Vector2i mousePosition) { }
        public void OnMouseMove(EditorContext context, GameState gameState, SelectionManager selectionManager, Vector2i mousePosition) { }
        public void Draw(EditorContext context, GameState gameState, SelectionManager selectionManager) { }
    }
}
