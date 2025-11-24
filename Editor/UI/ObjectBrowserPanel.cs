using Core;
using ImGuiNET;
using System;

namespace Editor.UI
{
    public class ObjectBrowserPanel
    {
        private readonly ObjectTypeManager _objectTypeManager;
        public Action<ObjectType>? OnObjectTypeSelected;
        private ObjectType? _selectedObjectType;

        public ObjectBrowserPanel(ObjectTypeManager objectTypeManager)
        {
            _objectTypeManager = objectTypeManager;
        }

        public void Draw()
        {
            ImGui.Begin("Object Types");
            foreach (var objectType in _objectTypeManager.GetAllObjectTypes())
            {
                if (ImGui.Selectable(objectType.Name, _selectedObjectType == objectType))
                {
                    _selectedObjectType = objectType;
                    OnObjectTypeSelected?.Invoke(objectType);
                }
            }
            ImGui.End();
        }
    }
}
