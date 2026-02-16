using Shared;
using ImGuiNET;

namespace Editor.UI
{
    public class ObjectBrowserPanel
    {
        private readonly IObjectTypeManager _objectTypeManager;
        private readonly EditorContext _editorContext;
        private string _searchString = "";

        public ObjectBrowserPanel(IObjectTypeManager objectTypeManager, EditorContext editorContext)
        {
            _objectTypeManager = objectTypeManager;
            _editorContext = editorContext;
        }

        public void Draw()
        {
            ImGui.Begin("Object Types");
            ImGui.InputText("Search", ref _searchString, 256);
            ImGui.Separator();

            foreach (var objectType in _objectTypeManager.GetAllObjectTypes())
            {
                if (!string.IsNullOrEmpty(_searchString) && !objectType.Name.Contains(_searchString, System.StringComparison.OrdinalIgnoreCase))
                    continue;

                if (ImGui.Selectable(objectType.Name, _editorContext.SelectedObjectType == objectType))
                {
                    _editorContext.SelectedObjectType = objectType;
                }

                if (ImGui.BeginDragDropSource())
                {
                    var bytes = System.Text.Encoding.UTF8.GetBytes(objectType.Name);
                    var ptr = System.Runtime.InteropServices.Marshal.AllocHGlobal(bytes.Length);
                    System.Runtime.InteropServices.Marshal.Copy(bytes, 0, ptr, bytes.Length);
                    ImGui.SetDragDropPayload("OBJECT_TYPE_PAYLOAD", ptr, (uint)bytes.Length);
                    ImGui.Text(objectType.Name);
                    ImGui.EndDragDropSource();
                    System.Runtime.InteropServices.Marshal.FreeHGlobal(ptr);
                }
            }
            ImGui.End();
        }
    }
}
