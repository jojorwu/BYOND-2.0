using Core;

namespace Editor
{
    public class SelectionManager
    {
        public GameObject? SelectedObject { get; private set; }

        public void Select(GameObject gameObject)
        {
            SelectedObject = gameObject;
        }

        public void Deselect()
        {
            SelectedObject = null;
        }
    }
}
