using Core;
using ImGuiNET;

namespace Editor.UI
{
    public class SceneHierarchyPanel
    {
        private readonly IGameApi _gameApi;
        private readonly SelectionManager _selectionManager;
        private string _searchString = "";

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
                            _selectionManager.SetSelection(gameObject);
                        }
                    }
                }
            }

            ImGui.End();
        }
    }
}
