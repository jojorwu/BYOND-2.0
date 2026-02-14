using Shared;
using System.Collections.Generic;
using System.Linq;

namespace Editor
{
    public class SelectionManager
    {
        private readonly HashSet<IGameObject> _selection = new();

        public IReadOnlyCollection<IGameObject> Selection => _selection;
        public IGameObject? SelectedObject => _selection.FirstOrDefault();
        public bool HasSelection => _selection.Count > 0;
        public int SelectionCount => _selection.Count;

        public void Select(IGameObject gameObject, bool clearExisting = true)
        {
            if (clearExisting) _selection.Clear();
            _selection.Add(gameObject);
        }

        public void ToggleSelection(IGameObject gameObject)
        {
            if (!_selection.Remove(gameObject))
            {
                _selection.Add(gameObject);
            }
        }

        public void AddToSelection(IGameObject gameObject)
        {
            _selection.Add(gameObject);
        }

        public void RemoveFromSelection(IGameObject gameObject)
        {
            _selection.Remove(gameObject);
        }

        public void Deselect()
        {
            _selection.Clear();
        }

        public bool IsSelected(IGameObject gameObject) => _selection.Contains(gameObject);
    }
}
