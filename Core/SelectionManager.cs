namespace Core
{
    /// <summary>
    /// Manages the selection of game objects.
    /// </summary>
    public class SelectionManager
    {
        /// <summary>
        /// Gets the currently selected game object.
        /// </summary>
        public GameObject? SelectedObject { get; private set; }

        /// <summary>
        /// Selects the specified game object.
        /// </summary>
        /// <param name="gameObject">The game object to select.</param>
        public void Select(GameObject gameObject)
        {
            SelectedObject = gameObject;
        }

        /// <summary>
        /// Deselects the currently selected game object.
        /// </summary>
        public void Deselect()
        {
            SelectedObject = null;
        }

        /// <summary>
        /// Clears the selection.
        /// </summary>
        public void Clear()
        {
            SelectedObject = null;
        }
    }
}
