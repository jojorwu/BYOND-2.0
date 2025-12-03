using Core;
using ImGuiNET;

namespace Editor.UI
{
    public class SceneHierarchyPanel
    {
        private readonly IGameApi _gameApi;
        private readonly SelectionManager _selectionManager;

        public SceneHierarchyPanel(IGameApi gameApi, SelectionManager selectionManager)
        {
            _gameApi = gameApi;
            _selectionManager = selectionManager;
        }

        public void Draw()
        {
            ImGui.Begin("Scene Hierarchy");

            var map = _gameApi.Map.GetMap();
            if (map != null)
            {
                foreach (var gameObject in map.GetAllGameObjects())
                {
                    var label = $"{gameObject.ObjectType.Name} ({gameObject.Id})";
                    if (ImGui.Selectable(label, _selectionManager.SelectedObject == gameObject))
                    {
                        _selectionManager.SetSelection(gameObject);
                    }
                }
            }

            ImGui.End();
        }
    }
}
