using Core;
using ImGuiNET;
using System.Collections.Generic;

namespace Editor.UI
{
    public class InspectorPanel
    {
        private readonly GameApi _gameApi;
        private readonly SelectionManager _selectionManager;
        private readonly EditorContext _editorContext;

        public InspectorPanel(GameApi gameApi, SelectionManager selectionManager, EditorContext editorContext)
        {
            _gameApi = gameApi;
            _selectionManager = selectionManager;
            _editorContext = editorContext;
        }

        public void Draw()
        {
            ImGui.Begin("Inspector");
            var selectedObject = _selectionManager.SelectedObject;
            if (selectedObject != null)
            {
                ImGui.LabelText("ID", selectedObject.Id.ToString());
                ImGui.LabelText("Type", selectedObject.ObjectType.Name);

                int[] position = { selectedObject.X, selectedObject.Y, selectedObject.Z };
                if (ImGui.InputInt3("Position", ref position[0]))
                {
                    selectedObject.SetPosition(position[0], position[1], position[2]);
                }

                var allProperties = new Dictionary<string, object>(selectedObject.ObjectType.DefaultProperties);
                foreach (var prop in selectedObject.Properties)
                {
                    allProperties[prop.Key] = prop.Value;
                }

                foreach (var prop in allProperties)
                {
                    string valueStr = prop.Value.ToString() ?? "";
                    if (ImGui.InputText(prop.Key, ref valueStr, 256))
                    {
                        selectedObject.Properties[prop.Key] = valueStr;
                    }
                }
            }
            else
            {
                ImGui.Text("No object selected.");
            }
            ImGui.End();
        }
    }
}
