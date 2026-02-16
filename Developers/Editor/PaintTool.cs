using Shared;
using Robust.Shared.Maths;
using Editor.History;

namespace Editor
{
    public class PaintTool : ITool
    {
        private readonly HistoryManager _historyManager;

        public string Name => "Paint";

        public PaintTool(HistoryManager historyManager)
        {
            _historyManager = historyManager;
        }

        public void OnSelected(EditorContext context) { }
        public void OnDeselected(EditorContext context) { }

        public void OnMouseDown(EditorContext context, GameState gameState, SelectionManager selectionManager, Vector2i mousePosition)
        {
            if (gameState.Map == null || context.SelectedObjectType == null) return;

            var tileX = mousePosition.X / EditorConstants.TileSize;
            var tileY = mousePosition.Y / EditorConstants.TileSize;

            var command = new PlaceObjectCommand(gameState, context.SelectedObjectType, tileX, tileY, context.CurrentZLevel);
            _historyManager.ExecuteCommand(command);
        }

        public void OnMouseUp(EditorContext context, GameState gameState, SelectionManager selectionManager, Vector2i mousePosition) { }
        public void OnMouseMove(EditorContext context, GameState gameState, SelectionManager selectionManager, Vector2i mousePosition) { }
        public void Draw(EditorContext context, GameState gameState, SelectionManager selectionManager) { }
    }
}
