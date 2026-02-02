using Shared;
using Shared.Models;
using Shared.Services;
using Shared.Interfaces;
using Shared.Enums;
using Shared.Operations;

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
