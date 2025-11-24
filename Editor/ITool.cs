using Core;
using Silk.NET.Maths;

namespace Editor
{
    public interface ITool
    {
        string Name { get; }
        void Activate(EditorContext editorContext);
        void Deactivate(EditorContext editorContext);
        void OnMouseDown(EditorContext editorContext, GameState gameState, SelectionManager selectionManager, Vector2D<int> mousePosition);
        void OnMouseUp(EditorContext editorContext, GameState gameState, SelectionManager selectionManager, Vector2D<int> mousePosition);
        void OnMouseMove(EditorContext editorContext, GameState gameState, SelectionManager selectionManager, Vector2D<int> mousePosition);
        void Draw(EditorContext editorContext, GameState gameState, SelectionManager selectionManager);
    }
}
