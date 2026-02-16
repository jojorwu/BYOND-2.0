using Shared;
using Editor;
using Editor.History;
using ImGuiNET;
using Silk.NET.OpenGL;
using System.Collections.Generic;
using Core;

namespace Editor.UI
{
    public class InspectorPanel
    {
        private readonly IGameApi _gameApi;
        private readonly SelectionManager _selectionManager;
        private readonly EditorContext _editorContext;
        private readonly AssetBrowserPanel _assetBrowserPanel;
        private readonly TextureManager _textureManager;
        private readonly HistoryManager _historyManager;

        public InspectorPanel(IGameApi gameApi, SelectionManager selectionManager, EditorContext editorContext, AssetBrowserPanel assetBrowserPanel, TextureManager textureManager, HistoryManager historyManager)
        {
            _gameApi = gameApi;
            _selectionManager = selectionManager;
            _editorContext = editorContext;
            _assetBrowserPanel = assetBrowserPanel;
            _textureManager = textureManager;
            _historyManager = historyManager;
        }

        public void Draw()
        {
            ImGui.Begin("Inspector");

            if (!string.IsNullOrEmpty(_assetBrowserPanel.SelectedFile))
            {
                var fileInfo = new System.IO.FileInfo(_assetBrowserPanel.SelectedFile);
                if (ImGui.CollapsingHeader("File Information", ImGuiTreeNodeFlags.DefaultOpen))
                {
                    ImGui.LabelText("Name", fileInfo.Name);
                    ImGui.LabelText("Path", fileInfo.FullName);
                    ImGui.LabelText("Size", $"{fileInfo.Length} bytes");
                }

                var extension = fileInfo.Extension.ToLowerInvariant();
                if (extension == ".png" || extension == ".jpg" || extension == ".jpeg" || extension == ".bmp")
                {
                    if (ImGui.CollapsingHeader("Preview", ImGuiTreeNodeFlags.DefaultOpen))
                    {
                        uint textureId = _textureManager.GetTexture(fileInfo.FullName);
                        if (textureId != 0)
                        {
                            ImGui.Image((IntPtr)textureId, new System.Numerics.Vector2(128, 128));
                        }
                    }
                }
            }
            else
            {
                if (_selectionManager.SelectionCount > 1)
                {
                    ImGui.Text($"{_selectionManager.SelectionCount} objects selected.");
                    if (ImGui.Button("Deselect All")) _selectionManager.Deselect();

                    // Here we could add batch editing logic
                }
                else if (_selectionManager.SelectedObject != null)
                {
                    var selectedObject = _selectionManager.SelectedObject;
                    if (ImGui.CollapsingHeader("General", ImGuiTreeNodeFlags.DefaultOpen))
                    {
                        ImGui.LabelText("ID", selectedObject.Id.ToString());
                        ImGui.LabelText("Type", selectedObject.ObjectType.Name);
                    }

                    if (ImGui.CollapsingHeader("Transform", ImGuiTreeNodeFlags.DefaultOpen))
                    {
                        int[] position = { selectedObject.X, selectedObject.Y, selectedObject.Z };
                        if (ImGui.InputInt3("Position", ref position[0], ImGuiInputTextFlags.EnterReturnsTrue))
                        {
                            var command = new MoveObjectCommand(selectedObject, selectedObject.X, selectedObject.Y, selectedObject.Z, position[0], position[1], position[2]);
                            _historyManager.ExecuteCommand(command);
                        }
                    }

                    if (ImGui.CollapsingHeader("Variables", ImGuiTreeNodeFlags.DefaultOpen))
                    {
                        for (int i = 0; i < selectedObject.ObjectType.VariableNames.Count; i++)
                        {
                            var varName = selectedObject.ObjectType.VariableNames[i];
                            var varValue = selectedObject.GetVariable(i);

                            if (varValue.TryGetValue(out DreamResource? resource) && resource != null)
                            {
                                ImGui.Text($"{varName}: {resource.Type} ('{resource.Path}')");
                            }
                            else
                            {
                                string valueStr = varValue.ToString();
                                if (ImGui.InputText(varName, ref valueStr, 256, ImGuiInputTextFlags.EnterReturnsTrue))
                                {
                                    var command = new ChangePropertyCommand(selectedObject, varName, varValue, new DreamValue(valueStr));
                                    _historyManager.ExecuteCommand(command);
                                }
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
