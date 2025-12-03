using Core;
using ImGuiNET;
using System;
using System.Threading.Tasks;
using NativeFileDialogNET;

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
                    if (ImGui.MenuItem("Import Project..."))
                    {
                        ImGui.OpenPopup("ImportProjectDlgKey");
                    }
                    ImGui.EndMenu();
                }

                if (ImGui.BeginMenu("Edit"))
                {
                    if (ImGui.MenuItem("Project Settings..."))
                    {
                        ImGui.OpenPopup("ProjectSettingsDlgKey");
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

            if (ImGui.BeginPopupModal("ProjectSettingsDlgKey"))
            {
                ImGui.Text("Project settings will be available here in the future.");
                ImGui.Text("Render settings and other options will be configurable.");
                if (ImGui.Button("Close"))
                {
                    ImGui.CloseCurrentPopup();
                }
                ImGui.EndPopup();
            }

            if (ImGui.BeginPopupModal("ChooseDmmFileDlgKey"))
            {
                using var dialog = new NativeFileDialog().SelectFile().AddFilter("DMM Files", "*.dmm");
                if (dialog.ShowDialog() == NativeFileDialog.Result.Okay)
                {
                    _dmmService.LoadDmm(dialog.Path);
                }
                ImGui.CloseCurrentPopup();
                ImGui.EndPopup();
            }

            if (ImGui.BeginPopupModal("ImportProjectDlgKey"))
            {
                using var dialog = new NativeFileDialog().SelectFolder();
                if (dialog.ShowDialog() == NativeFileDialog.Result.Okay)
                {
                    var configPath = System.IO.Path.Combine(dialog.Path, "server_config.json");
                    if (System.IO.File.Exists(configPath))
                    {
                        try
                        {
                            var json = System.IO.File.ReadAllText(configPath);
                            var settings = System.Text.Json.JsonSerializer.Deserialize<Core.ServerSettings>(json);
                            if (settings != null)
                            {
                                _editorContext.ServerSettings = settings;
                                Console.WriteLine("Project settings imported successfully.");
                                ImGui.OpenPopup("ImportSuccess");
                            }
                            else
                            {
                                throw new Exception("Deserialized settings were null.");
                            }
                        }
                        catch (Exception e)
                        {
                            Console.WriteLine($"[ERROR] Failed to import project settings: {e.Message}");
                            ImGui.OpenPopup("ImportError");
                        }
                    }
                    else
                    {
                        ImGui.OpenPopup("ImportError");
                    }
                }
                ImGui.CloseCurrentPopup();
                ImGui.EndPopup();
            }

            if (ImGui.BeginPopupModal("ImportSuccess"))
            {
                ImGui.Text("Project settings imported successfully!");
                if (ImGui.Button("OK"))
                {
                    ImGui.CloseCurrentPopup();
                }
                ImGui.EndPopup();
            }

            if (ImGui.BeginPopupModal("ImportError"))
            {
                ImGui.Text("Failed to import project settings.");
                ImGui.Text("Ensure server_config.json exists in the selected directory.");
                if (ImGui.Button("OK"))
                {
                    ImGui.CloseCurrentPopup();
                }
                ImGui.EndPopup();
            }
        }
    }
}
