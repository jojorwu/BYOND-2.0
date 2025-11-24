using System;
using Core;
using Silk.NET.Maths;

namespace Editor
{
    public class PaintTool : ITool
    {
        public string Name => "Paint";

        public void Activate(EditorContext editorContext)
        {
            Console.WriteLine("Paint Tool Activated");
        }


        public void Deactivate(EditorContext editorContext)
        {
            Console.WriteLine("Paint Tool Deactivated");
        }

        public void OnMouseDown(EditorContext editorContext, GameState gameState, SelectionManager selectionManager, Vector2D<int> mousePosition)
        {
            if (gameState.Map == null || editorContext.SelectedObjectType == null) return;

            int tileX = mousePosition.X / Constants.TileSize;
            int tileY = mousePosition.Y / Constants.TileSize;

            if (tileX >= 0 && tileX < gameState.Map.Width && tileY >= 0 && tileY < gameState.Map.Height)
            {
                var turf = gameState.Map.GetTurf(tileX, tileY, editorContext.CurrentZLevel);
                if (turf != null)
                {
                    var newObject = new GameObject(editorContext.SelectedObjectType, tileX, tileY, editorContext.CurrentZLevel);
                    turf.Contents.Add(newObject);
                    Console.WriteLine($"Placed '{newObject.ObjectType.Name}' at ({tileX}, {tileY})");
                }
            }
        }

        public void OnMouseUp(EditorContext editorContext, GameState gameState, SelectionManager selectionManager, Vector2D<int> mousePosition)
        {
        }

        public void OnMouseMove(EditorContext editorContext, GameState gameState, SelectionManager selectionManager, Vector2D<int> mousePosition)
        {
        }

        public void Draw(EditorContext editorContext, GameState gameState, SelectionManager selectionManager)
        {
        }
    }
}
