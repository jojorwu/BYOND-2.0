using System;
using System.Linq;
using Core;
using ImGuiNET;
using Silk.NET.Maths;

namespace Editor
{
    public class SelectionTool : ITool
    {
        public string Name => "Select";

        public void Activate(Editor editor)
        {
            Console.WriteLine("Selection Tool Activated");
        }

        public void Deactivate(Editor editor)
        {
            Console.WriteLine("Selection Tool Deactivated");
        }

        public void OnMouseDown(Editor editor, GameState gameState, SelectionManager selectionManager, Vector2D<int> mousePosition)
        {
            if (gameState.Map == null) return;

            int tileX = mousePosition.X / Constants.TileSize;
            int tileY = mousePosition.Y / Constants.TileSize;

            if (tileX >= 0 && tileX < gameState.Map.Width && tileY >= 0 && tileY < gameState.Map.Height)
            {
                var turf = gameState.Map.GetTurf(tileX, tileY, editor.CurrentZLevel);
                if (turf != null && turf.Contents.Any())
                {
                    selectionManager.Select(turf.Contents.First());
                }
                else
                {
                    selectionManager.Deselect();
                }
            }
        }

        public void OnMouseUp(Editor editor, GameState gameState, SelectionManager selectionManager, Vector2D<int> mousePosition)
        {
        }

        public void OnMouseMove(Editor editor, GameState gameState, SelectionManager selectionManager, Vector2D<int> mousePosition)
        {
        }

        public void Draw(Editor editor, GameState gameState, SelectionManager selectionManager)
        {
            var selectedObject = selectionManager.SelectedObject;
            if (selectedObject != null)
            {
                var windowPos = ImGui.GetWindowPos();
                var drawList = ImGui.GetWindowDrawList();

                var min = new System.Numerics.Vector2(
                    windowPos.X + selectedObject.X * Constants.TileSize,
                    windowPos.Y + selectedObject.Y * Constants.TileSize
                );
                var max = new System.Numerics.Vector2(
                    min.X + Constants.TileSize,
                    min.Y + Constants.TileSize
                );

                drawList.AddRect(min, max, 0xFF00FFFF, 0, ImDrawFlags.None, 2.0f);
            }
        }
    }
}
