using Editor.UI;

namespace Editor
{
    public interface IUIService
    {
        void SetActiveTab(EditorTab tab);
        EditorTab GetActiveTab();
    }
}
