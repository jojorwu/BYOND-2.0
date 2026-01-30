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
