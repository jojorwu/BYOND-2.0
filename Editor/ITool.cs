using Core;
using Silk.NET.Maths;

namespace Editor
{
    public interface ITool
    {
        string Name { get; }
        void Activate(Editor editor, EditorContext editorContext);
        void Deactivate(Editor editor, EditorContext editorContext);
        void OnMouseDown(Editor editor, EditorContext editorContext, GameApi gameApi, GameState gameState, SelectionManager selectionManager, Vector2D<int> mousePosition);
        void OnMouseUp(Editor editor, EditorContext editorContext, GameApi gameApi, GameState gameState, SelectionManager selectionManager, Vector2D<int> mousePosition);
        void OnMouseMove(Editor editor, EditorContext editorContext, GameApi gameApi, GameState gameState, SelectionManager selectionManager, Vector2D<int> mousePosition);
        void Draw(Editor editor, EditorContext editorContext, GameApi gameApi, GameState gameState, SelectionManager selectionManager);
    }
}
