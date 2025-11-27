using Core;
using ImGuiNET;

namespace Editor.UI
{
    public class MenuBarPanel
    {
        private readonly GameApi _gameApi;
        private readonly EditorContext _editorContext;
        private readonly BuildService _buildService;

        public MenuBarPanel(GameApi gameApi, EditorContext editorContext, BuildService buildService)
        {
            _gameApi = gameApi;
            _editorContext = editorContext;
            _buildService = buildService;
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
                    if (ImGui.MenuItem("Load DMM Map"))
                    {
                        // For now, hardcode the path. In the future, this would open a file dialog.
                        _gameApi.LoadDmmMap("maps/default.dmm");
                    }
                    ImGui.EndMenu();
                }

                if (ImGui.BeginMenu("Build"))
                {
                    if (ImGui.MenuItem("Build Project"))
                    {
                        _buildService.CompileProject();
                    }
                    ImGui.EndMenu();
                }
                ImGui.EndMainMenuBar();
            }
        }
    }
}
