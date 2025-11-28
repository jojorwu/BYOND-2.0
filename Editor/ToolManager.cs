using Core;
using Silk.NET.Maths;
using System.Collections.Generic;
using System.Linq;

namespace Editor
{
    public class ToolManager
    {
        public List<ITool> Tools { get; } = new();
        private ITool? _activeTool;

        public ToolManager()
        {
            Tools.Add(new SelectionTool());
            Tools.Add(new PaintTool());
        }

        public void SetActiveTool(ITool? tool, EditorContext context)
        {
            _activeTool?.OnDeselected(context);
            _activeTool = tool;
            _activeTool?.OnSelected(context);
        }

        public ITool? GetActiveTool() => _activeTool;

        public void OnMouseDown(EditorContext context, GameState gameState, SelectionManager selectionManager, Vector2D<int> mousePosition)
        {
            _activeTool?.OnMouseDown(context, gameState, selectionManager, mousePosition);
        }

        public void OnMouseUp(EditorContext context, GameState gameState, SelectionManager selectionManager, Vector2D<int> mousePosition)
        {
            _activeTool?.OnMouseUp(context, gameState, selectionManager, mousePosition);
        }

        public void OnMouseMove(EditorContext context, GameState gameState, SelectionManager selectionManager, Vector2D<int> mousePosition)
        {
            _activeTool?.OnMouseMove(context, gameState, selectionManager, mousePosition);
        }

        public void Draw(EditorContext context, GameState gameState, SelectionManager selectionManager)
        {
            _activeTool?.Draw(context, gameState, selectionManager);
        }
    }
}
