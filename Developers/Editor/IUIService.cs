using Shared.Models;
using Shared.Interfaces;
using Shared.Enums;
using Shared.Operations;
using Shared.Services;
using Editor.UI;

namespace Editor
{
    public interface IUIService
    {
        void SetActiveTab(EditorTab tab);
        EditorTab GetActiveTab();
    }
}
