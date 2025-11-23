using System.Collections.Generic;
using System.Linq;
using Core;
using Silk.NET.Maths;

namespace Editor
{
    public class ToolManager
    {
        private readonly List<ITool> _tools = new List<ITool>();
        private ITool? _activeTool;

        public IReadOnlyList<ITool> Tools => _tools;
        public ITool? ActiveTool => _activeTool;

        public ToolManager()
        {
            _tools.Add(new SelectionTool());
            _tools.Add(new PaintTool());
        }

        public void SetActiveTool(ITool? tool, Editor editor)
        {
            if (_activeTool == tool)
                return;

            _activeTool?.Deactivate(editor);
            _activeTool = tool;
            _activeTool?.Activate(editor);
        }

        public void OnMouseDown(Editor editor, GameState gameState, SelectionManager selectionManager, Vector2D<int> mousePosition)
        {
            _activeTool?.OnMouseDown(editor, gameState, selectionManager, mousePosition);
        }

        public void OnMouseUp(Editor editor, GameState gameState, SelectionManager selectionManager, Vector2D<int> mousePosition)
        {
            _activeTool?.OnMouseUp(editor, gameState, selectionManager, mousePosition);
        }

        public void OnMouseMove(Editor editor, GameState gameState, SelectionManager selectionManager, Vector2D<int> mousePosition)
        {
            _activeTool?.OnMouseMove(editor, gameState, selectionManager, mousePosition);
        }

        public void Draw(Editor editor, GameState gameState, SelectionManager selectionManager)
        {
            _activeTool?.Draw(editor, gameState, selectionManager);
        }
    }
}
