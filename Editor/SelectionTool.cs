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

        public void Activate(Editor editor, EditorContext editorContext)
        {
            Console.WriteLine("Selection Tool Activated");
        }

        public void Deactivate(Editor editor, EditorContext editorContext)
        {
            Console.WriteLine("Selection Tool Deactivated");
        }

        public void OnMouseDown(Editor editor, EditorContext editorContext, GameApi gameApi, GameState gameState, SelectionManager selectionManager, Vector2D<int> mousePosition)
        {
            if (gameState.Map == null) return;

            int tileX = mousePosition.X / Constants.TileSize;
            int tileY = mousePosition.Y / Constants.TileSize;

            if (tileX >= 0 && tileX < gameState.Map.Width && tileY >= 0 && tileY < gameState.Map.Height)
            {
                var turf = gameState.Map.GetTurf(tileX, tileY, editor.CurrentZLevel);
                if (turf != null && turf.Contents.Any())
                {
                    // Simple selection: just grab the first object on the turf.
                    var objectToSelect = turf.Contents.First();
                    selectionManager.Select(objectToSelect);
                    Console.WriteLine($"Selected object {objectToSelect.Id}");
                }
                else
                {
                    selectionManager.Clear();
                    Console.WriteLine("Selection cleared.");
                }
            }
        }

        public void OnMouseUp(Editor editor, EditorContext editorContext, GameApi gameApi, GameState gameState, SelectionManager selectionManager, Vector2D<int> mousePosition)
        {
        }

        public void OnMouseMove(Editor editor, EditorContext editorContext, GameApi gameApi, GameState gameState, SelectionManager selectionManager, Vector2D<int> mousePosition)
        {
        }

        public void Draw(Editor editor, EditorContext editorContext, GameApi gameApi, GameState gameState, SelectionManager selectionManager)
        {
            if (selectionManager.SelectedObject != null)
            {
                var selected = selectionManager.SelectedObject;
                var windowPos = ImGui.GetWindowPos();
                var drawList = ImGui.GetWindowDrawList();

                var min = new System.Numerics.Vector2(
                    windowPos.X + selected.X * Constants.TileSize,
                    windowPos.Y + selected.Y * Constants.TileSize
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
