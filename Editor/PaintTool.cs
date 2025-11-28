using Core;
using Silk.NET.Maths;

namespace Editor
{
    public class PaintTool : ITool
    {
        public string Name => "Paint";

        public void OnSelected(EditorContext context) { }
        public void OnDeselected(EditorContext context) { }

        public void OnMouseDown(EditorContext context, GameState gameState, SelectionManager selectionManager, Vector2D<int> mousePosition)
        {
            if (gameState.Map == null || context.SelectedObjectType == null) return;

            var tileX = mousePosition.X / EditorConstants.TileSize;
            var tileY = mousePosition.Y / EditorConstants.TileSize;

            var newObject = new GameObject(context.SelectedObjectType);
            gameState.Map.GetTurf(tileX, tileY, context.CurrentZLevel)?.Contents.Add(newObject);
        }

        public void OnMouseUp(EditorContext context, GameState gameState, SelectionManager selectionManager, Vector2D<int> mousePosition) { }
        public void OnMouseMove(EditorContext context, GameState gameState, SelectionManager selectionManager, Vector2D<int> mousePosition) { }
        public void Draw(EditorContext context, GameState gameState, SelectionManager selectionManager) { }
    }
}
