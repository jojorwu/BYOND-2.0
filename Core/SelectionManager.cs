namespace Core
{
    /// <summary>
    /// Manages the selection of a single game object in the editor.
    /// </summary>
    public class SelectionManager
    {
        /// <summary>
        /// Gets the currently selected game object.
        /// </summary>
        public GameObject? SelectedObject { get; private set; }

        /// <summary>
        /// Selects a game object, deselecting any previously selected object.
        /// </summary>
        /// <param name="gameObject">The game object to select.</param>
        public void Select(GameObject gameObject)
        {
            SelectedObject = gameObject;
        }

        /// <summary>
        /// Deselects a game object if it is currently selected.
        /// </summary>
        /// <param name="gameObject">The game object to deselect.</param>
        public void Deselect(GameObject gameObject)
        {
            if (SelectedObject == gameObject)
            {
                SelectedObject = null;
            }
        }

        /// <summary>
        /// Clears the current selection.
        /// </summary>
        public void Clear()
        {
            SelectedObject = null;
        }
    }
}
