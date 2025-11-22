using System.Collections.Generic;

namespace Core
{
    /// <summary>
    /// Manages the selection of game objects in the editor.
    /// </summary>
    public class SelectionManager
    {
        private readonly List<GameObject> _selectedObjects = new List<GameObject>();

        /// <summary>
        /// Gets the list of currently selected game objects.
        /// </summary>
        public IReadOnlyList<GameObject> SelectedObjects => _selectedObjects.AsReadOnly();

        /// <summary>
        /// Selects a game object.
        /// </summary>
        /// <param name="gameObject">The game object to select.</param>
        public void Select(GameObject gameObject)
        {
            if (!_selectedObjects.Contains(gameObject))
            {
                _selectedObjects.Add(gameObject);
            }
        }

        /// <summary>
        /// Deselects a game object.
        /// </summary>
        /// <param name="gameObject">The game object to deselect.</param>
        public void Deselect(GameObject gameObject)
        {
            _selectedObjects.Remove(gameObject);
        }

        /// <summary>
        /// Clears the current selection.
        /// </summary>
        public void Clear()
        {
            _selectedObjects.Clear();
        }
    }
}
