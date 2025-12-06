using Core;

namespace Editor
{
    public class SelectionManager
    {
        public IGameObject? SelectedObject { get; private set; }

        public void Select(IGameObject gameObject)
        {
            SelectedObject = gameObject;
        }

        public void Deselect()
        {
            SelectedObject = null;
        }
    }
}
