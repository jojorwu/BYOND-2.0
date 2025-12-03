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
        private string _newProjectName = "NewProject";
        private string _newProjectPath = "";

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
                    if (ImGui.MenuItem("New Project..."))
                    {
                        _newProjectPath = System.IO.Directory.GetCurrentDirectory(); // Default path
                        ImGui.OpenPopup("NewProjectDlgKey");
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
                if (ImGui.BeginTabBar("SettingsTabs"))
                {
                    if (ImGui.BeginTabItem("Server Settings"))
                    {
                        var serverSettings = _editorContext.ServerSettings;
                        ImGui.InputText("Server Name", ref serverSettings.ServerName, 256);
                        ImGui.InputInt("Max Players", ref serverSettings.MaxPlayers);
                        ImGui.EndTabItem();
                    }
                    if (ImGui.BeginTabItem("Render Settings"))
                    {
                        var clientSettings = _editorContext.ClientSettings;
                        ImGui.InputInt("Resolution Width", ref clientSettings.ResolutionWidth);
                        ImGui.InputInt("Resolution Height", ref clientSettings.ResolutionHeight);
                        ImGui.Checkbox("Fullscreen", ref clientSettings.Fullscreen);
                        ImGui.Checkbox("VSync", ref clientSettings.VSync);
                        ImGui.Checkbox("Remove Background", ref clientSettings.RemoveBackground);
                        ImGui.EndTabItem();
                    }
                    ImGui.EndTabBar();
                }

                if (ImGui.Button("Save"))
                {
                    var serverConfigPath = System.IO.Path.Combine(_editorContext.ProjectRoot, "server_config.json");
                    var serverConfigJson = System.Text.Json.JsonSerializer.Serialize(_editorContext.ServerSettings, new System.Text.Json.JsonSerializerOptions { WriteIndented = true });
                    System.IO.File.WriteAllText(serverConfigPath, serverConfigJson);

                    var clientConfigPath = System.IO.Path.Combine(_editorContext.ProjectRoot, "client_config.json");
                    var clientConfigJson = System.Text.Json.JsonSerializer.Serialize(_editorContext.ClientSettings, new System.Text.Json.JsonSerializerOptions { WriteIndented = true });
                    System.IO.File.WriteAllText(clientConfigPath, clientConfigJson);
                    ImGui.CloseCurrentPopup();
                }
                ImGui.SameLine();
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
                // Step 1: Select source folder or zip
                using var sourceDialog = new NativeFileDialog().SelectFile().AddFilter("Project Folder or Zip", "zip");
                if (sourceDialog.ShowDialog() == NativeFileDialog.Result.Okay)
                {
                    // Step 2: Select destination folder
                    using var destDialog = new NativeFileDialog().SelectFolder();
                    if (destDialog.ShowDialog() == NativeFileDialog.Result.Okay)
                    {
                        var sourcePath = sourceDialog.Path;
                        var destPath = destDialog.Path;
                        var projectName = System.IO.Path.GetFileNameWithoutExtension(sourcePath);
                        var finalDestPath = System.IO.Path.Combine(destPath, projectName);

                        try
                        {
                            if (System.IO.Directory.Exists(sourcePath)) // It's a directory
                            {
                                CopyDirectory(sourcePath, finalDestPath);
                            }
                            else if (System.IO.Path.GetExtension(sourcePath).Equals(".zip", StringComparison.OrdinalIgnoreCase))
                            {
                                System.IO.Compression.ZipFile.ExtractToDirectory(sourcePath, finalDestPath);
                            }
                            else
                            {
                                throw new Exception("Unsupported file type for import.");
                            }

                            _editorContext.ProjectRoot = finalDestPath;
                            // Also load settings
                            var serverConfigPath = System.IO.Path.Combine(finalDestPath, "server_config.json");
                            if (System.IO.File.Exists(serverConfigPath))
                            {
                                var json = System.IO.File.ReadAllText(serverConfigPath);
                                var settings = System.Text.Json.JsonSerializer.Deserialize<Core.ServerSettings>(json);
                                if(settings != null) _editorContext.ServerSettings = settings;
                            }

                            var clientConfigPath = System.IO.Path.Combine(finalDestPath, "client_config.json");
                            if (System.IO.File.Exists(clientConfigPath))
                            {
                                var json = System.IO.File.ReadAllText(clientConfigPath);
                                var settings = System.Text.Json.JsonSerializer.Deserialize<Core.ClientSettings>(json);
                                if (settings != null) _editorContext.ClientSettings = settings;
                            }

                            Console.WriteLine($"Project imported to '{finalDestPath}'");
                            ImGui.OpenPopup("ImportSuccess");
                        }
                        catch(Exception e)
                        {
                            Console.WriteLine($"[ERROR] Failed to import project: {e.Message}");
                            ImGui.OpenPopup("ImportError");
                        }
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

            if (ImGui.BeginPopupModal("NewProjectDlgKey"))
            {
                ImGui.InputText("Project Name", ref _newProjectName, 256);
                ImGui.InputText("Project Path", ref _newProjectPath, 256, ImGuiInputTextFlags.ReadOnly);
                ImGui.SameLine();
                if (ImGui.Button("..."))
                {
                    using var dialog = new NativeFileDialog().SelectFolder();
                    if (dialog.ShowDialog() == NativeFileDialog.Result.Okay)
                    {
                        _newProjectPath = dialog.Path;
                    }
                }

                if (ImGui.Button("Create"))
                {
                    CreateProject(_newProjectName, _newProjectPath);
                    ImGui.CloseCurrentPopup();
                }
                ImGui.SameLine();
                if (ImGui.Button("Cancel"))
                {
                    ImGui.CloseCurrentPopup();
                }
                ImGui.EndPopup();
            }
        }

        private void CreateProject(string projectName, string projectPath)
        {
            try
            {
                var fullProjectPath = System.IO.Path.Combine(projectPath, projectName);
                if (System.IO.Directory.Exists(fullProjectPath))
                {
                    Console.WriteLine($"[ERROR] Directory already exists: {fullProjectPath}");
                    return;
                }

                System.IO.Directory.CreateDirectory(fullProjectPath);
                System.IO.Directory.CreateDirectory(System.IO.Path.Combine(fullProjectPath, "maps"));
                System.IO.Directory.CreateDirectory(System.IO.Path.Combine(fullProjectPath, "code"));
                System.IO.Directory.CreateDirectory(System.IO.Path.Combine(fullProjectPath, "assets"));

                var serverConfig = new ServerSettings();
                var serverConfigJson = System.Text.Json.JsonSerializer.Serialize(serverConfig, new System.Text.Json.JsonSerializerOptions { WriteIndented = true });
                System.IO.File.WriteAllText(System.IO.Path.Combine(fullProjectPath, "server_config.json"), serverConfigJson);

                var clientConfig = new ClientSettings();
                var clientConfigJson = System.Text.Json.JsonSerializer.Serialize(clientConfig, new System.Text.Json.JsonSerializerOptions { WriteIndented = true });
                System.IO.File.WriteAllText(System.IO.Path.Combine(fullProjectPath, "client_config.json"), clientConfigJson);

                var dmeContent = "// My awesome project\n#include <__DEFINES/std.dm>\n";
                System.IO.File.WriteAllText(System.IO.Path.Combine(fullProjectPath, $"{projectName}.dme"), dmeContent);

                _editorContext.ProjectRoot = fullProjectPath;
                _editorContext.ServerSettings = serverConfig;

                Console.WriteLine($"Project '{projectName}' created at '{fullProjectPath}'");
            }
            catch(Exception e)
            {
                Console.WriteLine($"[ERROR] Failed to create project: {e.Message}");
            }
        }

        private void CopyDirectory(string sourceDir, string destinationDir)
        {
            var dir = new System.IO.DirectoryInfo(sourceDir);
            if (!dir.Exists)
                throw new System.IO.DirectoryNotFoundException($"Source directory not found: {dir.FullName}");

            System.IO.Directory.CreateDirectory(destinationDir);

            foreach (System.IO.FileInfo file in dir.GetFiles())
            {
                string targetFilePath = System.IO.Path.Combine(destinationDir, file.Name);
                file.CopyTo(targetFilePath);
            }

            foreach (System.IO.DirectoryInfo subDir in dir.GetDirectories())
            {
                string newDestinationDir = System.IO.Path.Combine(destinationDir, subDir.Name);
                CopyDirectory(subDir.FullName, newDestinationDir);
            }
        }
    }
}
