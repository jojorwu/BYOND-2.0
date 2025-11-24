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

        public void SetActiveTool(ITool? tool, Editor editor, EditorContext editorContext)
        {
            if (_activeTool == tool)
                return;

            _activeTool?.Deactivate(editor, editorContext);
            _activeTool = tool;
            _activeTool?.Activate(editor, editorContext);
        }

        public void OnMouseDown(Editor editor, EditorContext editorContext, GameApi gameApi, GameState gameState, SelectionManager selectionManager, Vector2D<int> mousePosition)
        {
            _activeTool?.OnMouseDown(editor, editorContext, gameApi, gameState, selectionManager, mousePosition);
        }

        public void OnMouseUp(Editor editor, EditorContext editorContext, GameApi gameApi, GameState gameState, SelectionManager selectionManager, Vector2D<int> mousePosition)
        {
            _activeTool?.OnMouseUp(editor, editorContext, gameApi, gameState, selectionManager, mousePosition);
        }

        public void OnMouseMove(Editor editor, EditorContext editorContext, GameApi gameApi, GameState gameState, SelectionManager selectionManager, Vector2D<int> mousePosition)
        {
            _activeTool?.OnMouseMove(editor, editorContext, gameApi, gameState, selectionManager, mousePosition);
        }

        public void Draw(Editor editor, EditorContext editorContext, GameApi gameApi, GameState gameState, SelectionManager selectionManager)
        {
            _activeTool?.Draw(editor, editorContext, gameApi, gameState, selectionManager);
        }
    }
}
