using Core;
using ImGuiNET;

namespace Editor.UI
{
    public class ObjectBrowserPanel
    {
        private readonly ObjectTypeManager _objectTypeManager;
        private readonly EditorContext _editorContext;

        public ObjectBrowserPanel(ObjectTypeManager objectTypeManager, EditorContext editorContext)
        {
            _objectTypeManager = objectTypeManager;
            _editorContext = editorContext;
        }

        public void Draw()
        {
            ImGui.Begin("Object Types");
            foreach (var objectType in _objectTypeManager.GetAllObjectTypes())
            {
                if (ImGui.Selectable(objectType.Name, _editorContext.SelectedObjectType == objectType))
                {
                    _editorContext.SelectedObjectType = objectType;
                }
            }
            ImGui.End();
        }
    }
}
