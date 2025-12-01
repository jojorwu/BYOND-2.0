using Core;
using ImGuiNET;

namespace Editor.UI
{
    public class MenuBarPanel
    {
        private readonly GameApi _gameApi;
        private readonly EditorContext _editorContext;
        private readonly BuildService _buildService;
        private readonly DmmService _dmmService;

        public MenuBarPanel(GameApi gameApi, EditorContext editorContext, BuildService buildService, DmmService dmmService)
        {
            _gameApi = gameApi;
            _editorContext = editorContext;
            _buildService = buildService;
            _dmmService = dmmService;
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
                            _gameApi.SaveMapAsync("maps/default.json");
                        }
                    }
                    if (ImGui.MenuItem("Load Map"))
                    {
                        _gameApi.LoadMapAsync("maps/default.json");
                    }
                    if (ImGui.MenuItem("Load DMM Map"))
                    {
                        ImGui.OpenPopup("ChooseDmmFileDlgKey");
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

            if (ImGui.BeginPopupModal("ChooseDmmFileDlgKey"))
            {
                // TODO: File dialog
                ImGui.EndPopup();
            }
        }
    }
}
