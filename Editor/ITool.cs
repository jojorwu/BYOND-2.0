using Robust.Shared.Maths;
using Shared;

namespace Editor
{
    public interface ITool
    {
        string Name { get; }
        void OnSelected(EditorContext context);
        void OnDeselected(EditorContext context);
        void OnMouseDown(EditorContext context, GameState gameState, SelectionManager selectionManager, Vector2i mousePosition);
        void OnMouseUp(EditorContext context, GameState gameState, SelectionManager selectionManager, Vector2i mousePosition);
        void OnMouseMove(EditorContext context, GameState gameState, SelectionManager selectionManager, Vector2i mousePosition);
        void Draw(EditorContext context, GameState gameState, SelectionManager selectionManager);
    }
}
