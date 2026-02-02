using Shared.Models;
using Shared.Interfaces;
using Shared.Enums;
using Shared.Operations;
using Shared.Services;
using Editor.UI;

namespace Editor
{
    public class UIService : IUIService
    {
        private EditorTab _activeTab = EditorTab.Projects;

        public void SetActiveTab(EditorTab tab)
        {
            _activeTab = tab;
        }

        public EditorTab GetActiveTab()
        {
            return _activeTab;
        }
    }
}
