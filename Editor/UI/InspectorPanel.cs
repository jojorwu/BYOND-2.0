using Core;
using ImGuiNET;
using System.Collections.Generic;

namespace Editor.UI
{
    public class InspectorPanel
    {
        private readonly IGameApi _gameApi;
        private readonly SelectionManager _selectionManager;
        private readonly EditorContext _editorContext;
        private readonly AssetBrowserPanel _assetBrowserPanel;

        public InspectorPanel(IGameApi gameApi, SelectionManager selectionManager, EditorContext editorContext, AssetBrowserPanel assetBrowserPanel)
        {
            _gameApi = gameApi;
            _selectionManager = selectionManager;
            _editorContext = editorContext;
            _assetBrowserPanel = assetBrowserPanel;
        }

        public void Draw()
        {
            ImGui.Begin("Inspector");

            if (!string.IsNullOrEmpty(_assetBrowserPanel.SelectedFile))
            {
                var fileInfo = new System.IO.FileInfo(_assetBrowserPanel.SelectedFile);
                ImGui.Text("File Inspector");
                ImGui.Separator();
                ImGui.LabelText("Name", fileInfo.Name);
                ImGui.LabelText("Path", fileInfo.FullName);
                ImGui.LabelText("Size", $"{fileInfo.Length} bytes");
            }
            else
            {
                var selectedObject = _selectionManager.SelectedObject;
                if (selectedObject != null)
                {
                    ImGui.Text("Object Inspector");
                    ImGui.Separator();
                    ImGui.LabelText("ID", selectedObject.Id.ToString());
                    ImGui.LabelText("Type", selectedObject.ObjectType.Name);

                    int[] position = { selectedObject.X, selectedObject.Y, selectedObject.Z };
                    if (ImGui.InputInt3("Position", ref position[0]))
                    {
                        selectedObject.SetPosition(position[0], position[1], position[2]);
                    }

                    var allProperties = new Dictionary<string, object?>(selectedObject.ObjectType.DefaultProperties);
                    foreach (var prop in selectedObject.Properties)
                    {
                        allProperties[prop.Key] = prop.Value;
                    }

                    foreach (var prop in allProperties)
                    {
                        if (prop.Value is DreamResource resource)
                        {
                            ImGui.Text($"{prop.Key}: {resource.Type} ('{resource.Path}')");
                        }
                        else
                        {
                            string valueStr = prop.Value?.ToString() ?? "";
                            if (ImGui.InputText(prop.Key, ref valueStr, 256))
                            {
                                selectedObject.Properties[prop.Key] = valueStr;
                            }
                        }
                    }
                }
                else
                {
                    ImGui.Text("No object or file selected.");
                }
            }
            ImGui.End();
        }
    }
}
