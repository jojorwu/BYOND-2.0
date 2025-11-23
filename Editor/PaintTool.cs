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
            // Logic to run when the tool is activated
            Console.WriteLine("Paint Tool Activated");
        }

        public void Deactivate(Editor editor)
        {
            // Logic to run when the tool is deactivated
            Console.WriteLine("Paint Tool Deactivated");
        }

        public void OnMouseDown(Editor editor, GameState gameState, SelectionManager selectionManager, Vector2D<int> mousePosition)
        {
            if (gameState.Map == null || string.IsNullOrEmpty(editor.SelectedAsset)) return;

            int tileX = mousePosition.X / Constants.TileSize;
            int tileY = mousePosition.Y / Constants.TileSize;

            if (tileX >= 0 && tileX < gameState.Map.Width && tileY >= 0 && tileY < gameState.Map.Height)
            {
                var turf = gameState.Map.GetTurf(tileX, tileY, editor.CurrentZLevel);
                if (turf != null)
                {
                    var newObject = new GameObject
                    {
                        Name = System.IO.Path.GetFileNameWithoutExtension(editor.SelectedAsset),
                        SpritePath = editor.SelectedAsset
                    };
                    turf.Contents.Add(newObject);
                    Console.WriteLine($"Placed '{newObject.Name}' at ({tileX}, {tileY})");
                }
            }
        }

        public void OnMouseUp(Editor editor, GameState gameState, SelectionManager selectionManager, Vector2D<int> mousePosition)
        {
            // Handle mouse up event for painting
        }

        public void OnMouseMove(Editor editor, GameState gameState, SelectionManager selectionManager, Vector2D<int> mousePosition)
        {
            // Handle mouse move event for painting
        }

        public void Draw(Editor editor, GameState gameState, SelectionManager selectionManager)
        {
            // Draw paint-related visuals (e.g., brush cursor)
        }
    }
}
