using Core;

namespace Editor
{
    public class SelectionManager
    {
        public GameObject? SelectedObject { get; private set; }

        public void SetSelection(GameObject gameObject)
        {
            SelectedObject = gameObject;
        }

        public void ClearSelection()
        {
            SelectedObject = null;
        }
    }
}
