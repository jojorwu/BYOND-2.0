
using ImGuiNET;
using Core;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;

namespace Editor.UI
{
    public class InspectorPanel
    {
        private readonly SelectionManager _selectionManager;
        private readonly ObjectTypeManager _objectTypeManager;
        private readonly GameApi _gameApi;
        private readonly EditorContext _context;

        private bool _showAddPropertyDialog = false;
        private string _newPropertyName = string.Empty;
        private int _newPropertyTypeIndex = 0;
        private readonly string[] _propertyTypes = { "String", "Integer", "Float", "Boolean" };

        public InspectorPanel(SelectionManager selectionManager, ObjectTypeManager objectTypeManager, GameApi gameApi, EditorContext context)
        {
            _selectionManager = selectionManager;
            _objectTypeManager = objectTypeManager;
            _gameApi = gameApi;
            _context = context;
        }

        public void Draw()
        {
            ImGui.Begin("Inspector");
            var selectedObject = _selectionManager.SelectedObject;
            if (selectedObject != null)
            {
                DrawGameObjectInspector(selectedObject);
            }
            else if (_context.SelectedObjectType != null)
            {
                DrawObjectTypeInspector(_context.SelectedObjectType);
            }
            else
            {
                ImGui.TextDisabled("Select an object or type to inspect properties.");
            }
            ImGui.End();

            if (_showAddPropertyDialog)
            {
                DrawAddPropertyDialog(_context.SelectedObjectType);
            }
        }

        private void DrawGameObjectInspector(GameObject selectedObject)
        {
            ImGui.Text($"ID: {selectedObject.Id}");
            ImGui.Separator();
            ImGui.TextColored(new Vector4(0.3f, 1.0f, 0.3f, 1.0f), selectedObject.ObjectType.Name);
            ImGui.Separator();

            if (ImGui.CollapsingHeader("Transform", ImGuiTreeNodeFlags.DefaultOpen))
            {
                int[] pos = { selectedObject.X, selectedObject.Y, selectedObject.Z };
                if (ImGui.DragInt3("Position", ref pos[0], 0.1f))
                {
                    selectedObject.SetPosition(pos[0], pos[1], pos[2]);
                }
            }

            if (ImGui.CollapsingHeader("Properties", ImGuiTreeNodeFlags.DefaultOpen))
            {
                var allProps = new Dictionary<string, object>(selectedObject.ObjectType.DefaultProperties);
                foreach (var prop in selectedObject.Properties)
                {
                    allProps[prop.Key] = prop.Value;
                }

                foreach (var prop in allProps)
                {
                    string key = prop.Key;
                    object val = prop.Value;
                    if (val is int iVal) { if (ImGui.DragInt(key, ref iVal)) selectedObject.Properties[key] = iVal; }
                    else if (val is float fVal) { if (ImGui.DragFloat(key, ref fVal)) selectedObject.Properties[key] = fVal; }
                    else if (val is bool bVal) { if (ImGui.Checkbox(key, ref bVal)) selectedObject.Properties[key] = bVal; }
                    else
                    {
                        string sVal = val?.ToString() ?? "";
                        if (ImGui.InputText(key, ref sVal, 256)) selectedObject.Properties[key] = sVal;
                    }
                }
            }

            ImGui.Spacing();
            if (ImGui.Button("Delete Object", new Vector2(-1, 0)))
            {
                _gameApi.DestroyObject(selectedObject.Id);
                _selectionManager.Deselect(selectedObject);
            }
        }

        private void DrawObjectTypeInspector(ObjectType selectedObjectType)
        {
            ImGui.TextColored(new Vector4(0.3f, 1.0f, 0.3f, 1.0f), selectedObjectType.Name);
            ImGui.Separator();

            if (ImGui.CollapsingHeader("Default Properties", ImGuiTreeNodeFlags.DefaultOpen))
            {
                var properties = selectedObjectType.DefaultProperties;
                var keys = properties.Keys.ToList();
                foreach (var key in keys)
                {
                    var val = properties[key];
                    bool changed = false;
                    if (val is int iVal)
                    {
                        if (ImGui.DragInt(key, ref iVal))
                        {
                            properties[key] = iVal;
                            changed = true;
                        }
                    }
                    else if (val is float fVal)
                    {
                        if (ImGui.DragFloat(key, ref fVal))
                        {
                            properties[key] = fVal;
                            changed = true;
                        }
                    }
                    else if (val is bool bVal)
                    {
                        if (ImGui.Checkbox(key, ref bVal))
                        {
                            properties[key] = bVal;
                            changed = true;
                        }
                    }
                    else
                    {
                        string sVal = val?.ToString() ?? "";
                        if (ImGui.InputText(key, ref sVal, 256))
                        {
                            properties[key] = sVal;
                            changed = true;
                        }
                    }
                    if (changed) _objectTypeManager.SaveTypes();
                }
            }

            ImGui.Spacing();
            if (ImGui.Button("Add Property..."))
            {
                _showAddPropertyDialog = true;
                _newPropertyName = string.Empty;
                _newPropertyTypeIndex = 0;
            }
        }

        private void DrawAddPropertyDialog(ObjectType? selectedObjectType)
        {
            if (selectedObjectType == null)
            {
                _showAddPropertyDialog = false;
                return;
            }

            ImGui.SetNextWindowSize(new Vector2(300, 120));
            ImGui.Begin("Add New Property", ref _showAddPropertyDialog, ImGuiWindowFlags.NoResize | ImGuiWindowFlags.NoCollapse);
            ImGui.InputText("Property Name", ref _newPropertyName, 64);
            ImGui.Combo("Property Type", ref _newPropertyTypeIndex, _propertyTypes, _propertyTypes.Length);
            ImGui.Spacing();

            if (ImGui.Button("Add", new Vector2(120, 0)))
            {
                if (!string.IsNullOrWhiteSpace(_newPropertyName) && !selectedObjectType.DefaultProperties.ContainsKey(_newPropertyName))
                {
                    object defaultValue = _propertyTypes[_newPropertyTypeIndex] switch
                    {
                        "Integer" => 0,
                        "Float" => 0.0f,
                        "Boolean" => false,
                        _ => ""
                    };
                    selectedObjectType.DefaultProperties.Add(_newPropertyName, defaultValue);
                    _objectTypeManager.SaveTypes();
                    _showAddPropertyDialog = false;
                }
            }

            ImGui.SameLine();
            if (ImGui.Button("Cancel", new Vector2(120, 0)))
            {
                _showAddPropertyDialog = false;
            }

            ImGui.End();
        }
    }
}
