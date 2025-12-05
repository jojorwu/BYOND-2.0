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

                if (ImGui.BeginDragDropSource())
                {
                    var objectTypeName = objectType.Name;
                    var bytes = System.Text.Encoding.UTF8.GetBytes(objectTypeName);
                    unsafe
                    {
                        fixed (byte* p = bytes)
                        {
                            ImGui.SetDragDropPayload("OBJECT_TYPE_PAYLOAD", (System.IntPtr)p, (uint)bytes.Length);
                        }
                    }
                    ImGui.Text(objectType.Name);
                    ImGui.EndDragDropSource();
                }
            }
            ImGui.End();
        }
    }
}
