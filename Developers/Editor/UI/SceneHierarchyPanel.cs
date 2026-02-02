using Shared;
using Shared.Models;
using Shared.Services;
using Shared.Interfaces;
using Shared.Enums;
using Shared.Operations;
using Editor;
using ImGuiNET;

namespace Editor.UI
{
    public class SceneHierarchyPanel
    {
        private readonly IGameApi _gameApi;
        private readonly SelectionManager _selectionManager;
        private string _searchString = "";
        private IGameObject? _objectToDelete = null;

        public SceneHierarchyPanel(IGameApi gameApi, SelectionManager selectionManager)
        {
            _gameApi = gameApi;
            _selectionManager = selectionManager;
        }

        public void Draw()
        {
            ImGui.Begin("Scene Hierarchy");

            ImGui.InputText("Search", ref _searchString, 256);
            ImGui.Separator();

            var map = _gameApi.Map.GetMap();
            if (map != null)
            {
                foreach (var gameObject in map.GetAllGameObjects())
                {
                    var label = $"{gameObject.ObjectType.Name} ({gameObject.Id})";
                    if (string.IsNullOrEmpty(_searchString) || label.Contains(_searchString, System.StringComparison.OrdinalIgnoreCase))
                    {
                        if (ImGui.Selectable(label, _selectionManager.SelectedObject == gameObject))
                        {
                        _selectionManager.Select(gameObject);
                        }
                        if (ImGui.BeginPopupContextItem($"ContextMenu_{gameObject.Id}"))
                        {
                            if (ImGui.MenuItem("Delete"))
                            {
                                _objectToDelete = gameObject;
                            }
                            ImGui.EndPopup();
                        }
                    }
                }
            }

            if (_objectToDelete != null)
            {
                ImGui.OpenPopup("DeleteConfirmation");
            }

            if (ImGui.BeginPopupModal("DeleteConfirmation"))
            {
                ImGui.Text($"Are you sure you want to delete '{_objectToDelete?.ObjectType.Name} ({_objectToDelete?.Id})'?");
                if (ImGui.Button("Yes"))
                {
                    if (_objectToDelete != null)
                    {
                        _gameApi.Objects.DestroyObject(_objectToDelete.Id);
                    }
                    if (_selectionManager.SelectedObject == _objectToDelete)
                    {
                        _selectionManager.Deselect();
                    }
                    _objectToDelete = null;
                    ImGui.CloseCurrentPopup();
                }
                ImGui.SameLine();
                if (ImGui.Button("No"))
                {
                    _objectToDelete = null;
                    ImGui.CloseCurrentPopup();
                }
                ImGui.EndPopup();
            }

            ImGui.End();
        }
    }
}
