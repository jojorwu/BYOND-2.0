
using ImGuiNET;
using Core;
using System;
using System.Linq;

namespace Editor.UI
{
    public class ObjectBrowserPanel
    {
        private readonly ObjectTypeManager _objectTypeManager;
        private readonly EditorContext _context;
        private string _objectBrowserFilter = string.Empty;

        public ObjectBrowserPanel(ObjectTypeManager objectTypeManager, EditorContext context)
        {
            _objectTypeManager = objectTypeManager;
            _context = context;
        }

        public void Draw()
        {
            ImGui.Begin("Object Tree");
            ImGui.InputText("Search", ref _objectBrowserFilter, 64);
            ImGui.Separator();

            var filteredTypes = _objectTypeManager.GetAllObjectTypes()
                .Where(t => t.Name.Contains(_objectBrowserFilter, StringComparison.OrdinalIgnoreCase))
                .OrderBy(t => t.Name)
                .ToList();

            foreach (var objectType in filteredTypes)
            {
                ImGuiTreeNodeFlags flags = ImGuiTreeNodeFlags.Leaf | ImGuiTreeNodeFlags.SpanAvailWidth;
                if (_context.SelectedObjectType == objectType)
                {
                    flags |= ImGuiTreeNodeFlags.Selected;
                }

                string icon = "[O] ";
                if (objectType.Name.Contains("mob")) icon = "[M] ";
                if (objectType.Name.Contains("turf")) icon = "[T] ";

                if (ImGui.TreeNodeEx(icon + objectType.Name, flags))
                {
                    if (ImGui.IsItemClicked())
                    {
                        _context.SelectedObjectType = objectType;
                    }
                    ImGui.TreePop();
                }
            }
            ImGui.End();
        }
    }
}
