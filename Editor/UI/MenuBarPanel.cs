using Core;
using ImGuiNET;
using System;
using System.Threading.Tasks;

namespace Editor.UI
{
    public class MenuBarPanel
    {
        private readonly IGameApi _gameApi;
        private readonly EditorContext _editorContext;
        private readonly BuildService _buildService;
        private readonly DmmService _dmmService;

        public MenuBarPanel(IGameApi gameApi, EditorContext editorContext, BuildService buildService, DmmService dmmService)
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
                        var map = _gameApi.Map.GetMap();
                        if (map != null)
                        {
                            Task.Run(async () => {
                                try
                                {
                                    await _gameApi.Map.SaveMapAsync("maps/default.json");
                                }
                                catch (Exception e)
                                {
                                    Console.WriteLine($"[ERROR] Failed to save map: {e.Message}");
                                }
                            });
                        }
                    }
                    if (ImGui.MenuItem("Load Map"))
                    {
                        Task.Run(async () => {
                            try
                            {
                                await _gameApi.Map.LoadMapAsync("maps/default.json");
                            }
                            catch (Exception e)
                            {
                                Console.WriteLine($"[ERROR] Failed to load map: {e.Message}");
                            }
                        });
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
