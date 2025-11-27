using System;
using Core;
using ImGuiNET;
using Silk.NET.Maths;
using System.Linq;

namespace Editor
{
    public class SelectionTool : ITool
    {
        public string Name => "Select";

        public void Activate(EditorContext editorContext)
        {
            Console.WriteLine("Selection Tool Activated");
        }

        public void Deactivate(EditorContext editorContext)
        {
            Console.WriteLine("Selection Tool Deactivated");
        }

        public void OnMouseDown(EditorContext editorContext, GameState gameState, SelectionManager selectionManager, Vector2D<int> mousePosition)
        {
            if (gameState.Map == null) return;

            int tileX = mousePosition.X / EditorConstants.TileSize;
            int tileY = mousePosition.Y / EditorConstants.TileSize;

            if (tileX >= 0 && tileX < gameState.Map.Width && tileY >= 0 && tileY < gameState.Map.Height)
            {
                var turf = gameState.Map.GetTurf(tileX, tileY, editorContext.CurrentZLevel);
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

        public void OnMouseUp(EditorContext editorContext, GameState gameState, SelectionManager selectionManager, Vector2D<int> mousePosition)
        {
        }

        public void OnMouseMove(EditorContext editorContext, GameState gameState, SelectionManager selectionManager, Vector2D<int> mousePosition)
        {
        }

        public void Draw(EditorContext editorContext, GameState gameState, SelectionManager selectionManager)
        {
            if (selectionManager.SelectedObject != null)
            {
                var selected = selectionManager.SelectedObject;
                var windowPos = ImGui.GetWindowPos();
                var drawList = ImGui.GetWindowDrawList();

                var min = new System.Numerics.Vector2(
                    windowPos.X + selected.X * EditorConstants.TileSize,
                    windowPos.Y + selected.Y * EditorConstants.TileSize
                );
                var max = new System.Numerics.Vector2(
                    min.X + EditorConstants.TileSize,
                    min.Y + EditorConstants.TileSize
                );

                drawList.AddRect(min, max, 0xFF00FFFF, 0, ImDrawFlags.None, 2.0f);
            }
        }
    }
}
