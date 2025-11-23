using System;
using Core;
using Silk.NET.Maths;

namespace Editor
{
    public class PaintTool : ITool
    {
        public string Name => "Paint";

        public void Activate(Editor editor)
        {
            Console.WriteLine("Paint Tool Activated");
        }

        public void Deactivate(Editor editor)
        {
            Console.WriteLine("Paint Tool Deactivated");
        }

        public void OnMouseDown(Editor editor, GameApi gameApi, GameState gameState, SelectionManager selectionManager, Vector2D<int> mousePosition)
        {
            if (gameState.Map == null || editor.SelectedObjectType == null) return;

            int tileX = mousePosition.X / Constants.TileSize;
            int tileY = mousePosition.Y / Constants.TileSize;

            if (tileX >= 0 && tileX < gameState.Map.Width && tileY >= 0 && tileY < gameState.Map.Height)
            {
                gameApi.CreateObject(editor.SelectedObjectType.Name, tileX, tileY, editor.CurrentZLevel);
            }
        }

        public void OnMouseUp(Editor editor, GameApi gameApi, GameState gameState, SelectionManager selectionManager, Vector2D<int> mousePosition)
        {
        }

        public void OnMouseMove(Editor editor, GameApi gameApi, GameState gameState, SelectionManager selectionManager, Vector2D<int> mousePosition)
        {
        }

        public void Draw(Editor editor, GameApi gameApi, GameState gameState, SelectionManager selectionManager)
        {
        }
    }
}
