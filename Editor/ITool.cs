using Core;
using Silk.NET.Maths;

namespace Editor
{
    public interface ITool
    {
        string Name { get; }
        void Activate(Editor editor);
        void Deactivate(Editor editor);
        void OnMouseDown(Editor editor, GameState gameState, SelectionManager selectionManager, Vector2D<int> mousePosition);
        void OnMouseUp(Editor editor, GameState gameState, SelectionManager selectionManager, Vector2D<int> mousePosition);
        void OnMouseMove(Editor editor, GameState gameState, SelectionManager selectionManager, Vector2D<int> mousePosition);
        void Draw(Editor editor, GameState gameState, SelectionManager selectionManager);
    }
}
