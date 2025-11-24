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

        public void SetActiveTool(ITool? tool, EditorContext editorContext)
        {
            if (_activeTool == tool)
                return;

            _activeTool?.Deactivate(editorContext);
            _activeTool = tool;
            _activeTool?.Activate(editorContext);
        }

        public void OnMouseDown(EditorContext editorContext, GameState gameState, SelectionManager selectionManager, Vector2D<int> mousePosition)
        {
            _activeTool?.OnMouseDown(editorContext, gameState, selectionManager, mousePosition);
        }

        public void OnMouseUp(EditorContext editorContext, GameState gameState, SelectionManager selectionManager, Vector2D<int> mousePosition)
        {
            _activeTool?.OnMouseUp(editorContext, gameState, selectionManager, mousePosition);
        }

        public void OnMouseMove(EditorContext editorContext, GameState gameState, SelectionManager selectionManager, Vector2D<int> mousePosition)
        {
            _activeTool?.OnMouseMove(editorContext, gameState, selectionManager, mousePosition);
        }

        public void Draw(EditorContext editorContext, GameState gameState, SelectionManager selectionManager)
        {
            _activeTool?.Draw(editorContext, gameState, selectionManager);
        }
    }
}
