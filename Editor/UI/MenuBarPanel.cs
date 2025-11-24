using Core;
using ImGuiNET;

namespace Editor.UI
{
    public class MenuBarPanel
    {
        private readonly GameApi _gameApi;
        private readonly EditorContext _editorContext;

        public MenuBarPanel(GameApi gameApi, EditorContext editorContext)
        {
            _gameApi = gameApi;
            _editorContext = editorContext;
        }

        public void Draw()
        {
            if (ImGui.BeginMainMenuBar())
            {
                if (ImGui.BeginMenu("File"))
                {
                    if (ImGui.MenuItem("Save Map"))
                    {
                        var map = _gameApi.GetMap();
                        if (map != null)
                        {
                            _gameApi.SaveMap("maps/default.json");
                        }
                    }
                    if (ImGui.MenuItem("Load Map"))
                    {
                        _gameApi.LoadMap("maps/default.json");
                    }
                    ImGui.EndMenu();
                }
                ImGui.EndMainMenuBar();
            }
        }
    }
}
