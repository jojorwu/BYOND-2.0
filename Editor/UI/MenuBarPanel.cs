using Shared;
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
        private readonly IDmmService _dmmService;
        private readonly LocalizationManager _localizationManager;
        private string _newProjectName = "NewProject";
        private string _newProjectPath = "";
        public string? ProjectToLoad { get; private set; }
        public bool IsExitRequested { get; private set; }

        public MenuBarPanel(IGameApi gameApi, EditorContext editorContext, BuildService buildService, IDmmService dmmService, LocalizationManager localizationManager)
        {
            _gameApi = gameApi;
            _editorContext = editorContext;
            _buildService = buildService;
            _dmmService = dmmService;
            _localizationManager = localizationManager;
        }

        public void SaveScene(Scene scene, bool saveAs)
        {
            if (saveAs || !System.IO.File.Exists(scene.FilePath))
            {
                using var dialog = new NativeFileDialog().SaveFile().AddFilter("Map Files", "dmm,json");
                DialogResult result = dialog.Open(out string? path);
                if (result == DialogResult.Okay && path != null)
                {
                    scene.FilePath = path;
                }
                else
                {
                    return;
                }
            }

            if (scene.GameState.Map != null && !string.IsNullOrEmpty(scene.FilePath))
            {
                Task.Run(async () =>
                {
                    _gameApi.Map.SetMap(scene.GameState.Map);
                    await _gameApi.Map.SaveMapAsync(scene.FilePath);
                    scene.IsDirty = false;
                });
            }
        }

        public void Draw()
        {
            IsExitRequested = false;
            ProjectToLoad = null;

            if (ImGui.BeginMainMenuBar())
            {
                if (ImGui.BeginMenu(_localizationManager.GetString("File")))
                {
                    if (ImGui.MenuItem(_localizationManager.GetString("New Scene")))
                    {
                        var newScene = new Scene("New Scene " + (_editorContext.OpenScenes.Count + 1))
                        {
                            GameState = { Map = new Map() },
                            IsDirty = true
                        };
                        _editorContext.OpenScenes.Add(newScene);
                        _editorContext.ActiveSceneIndex = _editorContext.OpenScenes.Count - 1;
                    }
                    if (ImGui.MenuItem(_localizationManager.GetString("Save Scene")))
                    {
                        var scene = _editorContext.GetActiveScene();
                        if (scene != null)
                        {
                            SaveScene(scene, false);
                        }
                    }
                    if (ImGui.MenuItem(_localizationManager.GetString("Save Scene As...")))
                    {
                        var scene = _editorContext.GetActiveScene();
                        if (scene != null)
                        {
                            SaveScene(scene, true);
                        }
                    }
                    ImGui.Separator();
                    if (ImGui.MenuItem(_localizationManager.GetString("Open...")))
                    {
                        ImGui.OpenPopup("ChooseDmmFileDlgKey");
                    }
                    if (ImGui.MenuItem(_localizationManager.GetString("New Project...")))
                    {
                        _newProjectPath = System.IO.Directory.GetCurrentDirectory(); // Default path
                        ImGui.OpenPopup("NewProjectDlgKey");
                    }
                    if (ImGui.MenuItem(_localizationManager.GetString("Import Project...")))
                    {
                        ImGui.OpenPopup("ImportProjectDlgKey");
                    }
                    if (ImGui.BeginMenu(_localizationManager.GetString("Recent Projects")))
                    {
                        foreach (var project in _editorContext.RecentProjects)
                        {
                            if (ImGui.MenuItem(project))
                            {
                                ProjectToLoad = project;
                            }
                        }
                        ImGui.EndMenu();
                    }
                    ImGui.Separator();
                    if (ImGui.MenuItem(_localizationManager.GetString("Exit")))
                    {
                        IsExitRequested = true;
                    }
                    ImGui.EndMenu();
                }

                if (ImGui.BeginMenu(_localizationManager.GetString("Edit")))
                {
                    if (ImGui.MenuItem(_localizationManager.GetString("Undo"))) { }
                    if (ImGui.MenuItem(_localizationManager.GetString("Redo"))) { }
                    ImGui.Separator();
                    if (ImGui.MenuItem(_localizationManager.GetString("Project Settings")))
                    {
                        ImGui.OpenPopup("ProjectSettingsDlgKey");
                    }
                    ImGui.EndMenu();
                }

                if (ImGui.BeginMenu(_localizationManager.GetString("Build")))
                {
                    if (ImGui.MenuItem(_localizationManager.GetString("Build Project")))
                    {
                        _buildService.CompileProject();
                    }
                    ImGui.EndMenu();
                }

                if (ImGui.BeginMenu(_localizationManager.GetString("Help")))
                {
                    if (ImGui.MenuItem(_localizationManager.GetString("About")))
                    {
                        ImGui.OpenPopup("AboutDlgKey");
                    }
                    ImGui.EndMenu();
                }

                ImGui.EndMainMenuBar();
            }

            DrawProjectSettingsModal();
            DrawChooseDmmFileModal();
            DrawImportProjectModal();
            DrawNewProjectModal();
            DrawAboutModal();
        }

        private void DrawProjectSettingsModal()
        {
            if (ImGui.BeginPopupModal("ProjectSettingsDlgKey"))
            {
                if (ImGui.BeginTabBar("SettingsTabs"))
                {
                    if (ImGui.BeginTabItem("Server Settings"))
                    {
                        var serverSettings = _editorContext.ServerSettings;
                        string serverName = serverSettings.ServerName;
                        int maxPlayers = serverSettings.MaxPlayers;
                        if (ImGui.InputText("Server Name", ref serverName, 256)) serverSettings.ServerName = serverName;
                        if (ImGui.InputInt("Max Players", ref maxPlayers)) serverSettings.MaxPlayers = maxPlayers;
                        ImGui.EndTabItem();
                    }
                    if (ImGui.BeginTabItem("Render Settings"))
                    {
                        var clientSettings = _editorContext.ClientSettings;
                        int resolutionWidth = clientSettings.ResolutionWidth;
                        int resolutionHeight = clientSettings.ResolutionHeight;
                        bool fullscreen = clientSettings.Fullscreen;
                        bool vsync = clientSettings.VSync;
                        bool removeBackground = clientSettings.RemoveBackground;
                        if (ImGui.InputInt("Resolution Width", ref resolutionWidth)) clientSettings.ResolutionWidth = resolutionWidth;
                        if (ImGui.InputInt("Resolution Height", ref resolutionHeight)) clientSettings.ResolutionHeight = resolutionHeight;
                        if (ImGui.Checkbox("Fullscreen", ref fullscreen)) clientSettings.Fullscreen = fullscreen;
                        if (ImGui.Checkbox("VSync", ref vsync)) clientSettings.VSync = vsync;
                        if (ImGui.Checkbox("Remove Background", ref removeBackground)) clientSettings.RemoveBackground = removeBackground;
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
        }

        private void DrawChooseDmmFileModal()
        {
            if (ImGui.BeginPopupModal("ChooseDmmFileDlgKey"))
            {
                using var dialog = new NativeFileDialog().SelectFile().AddFilter("Map Files", "dmm,yml,json");
                DialogResult result = dialog.Open(out string? path);
                if (result == DialogResult.Okay && path != null)
                {
                    Task.Run(async () =>
                    {
                        var map = await _dmmService.LoadMapAsync(path);
                        if (map != null)
                        {
                            var newScene = new Scene(System.IO.Path.GetFileName(path))
                            {
                                FilePath = path,
                                GameState = { Map = map },
                                IsDirty = false
                            };
                            _editorContext.OpenScenes.Add(newScene);
                            _editorContext.ActiveSceneIndex = _editorContext.OpenScenes.Count - 1;
                        }
                    });
                }
                ImGui.CloseCurrentPopup();
                ImGui.EndPopup();
            }
        }

        private void DrawImportProjectModal()
        {
            if (ImGui.BeginPopupModal("ImportProjectDlgKey"))
            {
                // Step 1: Select source folder or zip
                using var sourceDialog = new NativeFileDialog().SelectFile().AddFilter("Project Folder or Zip", "zip");
                DialogResult sourceResult = sourceDialog.Open(out string? sourcePath);
                if (sourceResult == DialogResult.Okay && sourcePath != null)
                {
                    // Step 2: Select destination folder
                    using var destDialog = new NativeFileDialog().SelectFolder();
                    DialogResult destResult = destDialog.Open(out string? destPath);
                    if (destResult == DialogResult.Okay && destPath != null)
                    {
                        var projectName = System.IO.Path.GetFileNameWithoutExtension(sourcePath);
                        var finalDestPath = System.IO.Path.Combine(destPath, projectName);

                        try
                        {
                            if (System.IO.Directory.Exists(sourcePath)) // It's a directory
                            {
                                CopyDirectory(sourcePath, finalDestPath);
                            }
                            else if (System.IO.Path.GetExtension(sourcePath)?.Equals(".zip", StringComparison.OrdinalIgnoreCase) ?? false)
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
                                var settings = System.Text.Json.JsonSerializer.Deserialize<ServerSettings>(json);
                                if (settings != null) _editorContext.ServerSettings = settings;
                            }

                            var clientConfigPath = System.IO.Path.Combine(finalDestPath, "client_config.json");
                            if (System.IO.File.Exists(clientConfigPath))
                            {
                                var json = System.IO.File.ReadAllText(clientConfigPath);
                                var settings = System.Text.Json.JsonSerializer.Deserialize<ClientSettings>(json);
                                if (settings != null) _editorContext.ClientSettings = settings;
                            }

                            Console.WriteLine($"Project imported to '{finalDestPath}'");
                            ImGui.OpenPopup("ImportSuccess");
                        }
                        catch (Exception e)
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
        }

        private void DrawNewProjectModal()
        {
            if (ImGui.BeginPopupModal("NewProjectDlgKey"))
            {
                ImGui.InputText("Project Name", ref _newProjectName, 256);
                ImGui.InputText("Project Path", ref _newProjectPath, 256, ImGuiInputTextFlags.ReadOnly);
                ImGui.SameLine();
                if (ImGui.Button("..."))
                {
                    using var dialog = new NativeFileDialog().SelectFolder();
                    DialogResult result = dialog.Open(out string? path);
                    if (result == DialogResult.Okay && path != null)
                    {
                        _newProjectPath = path;
                    }
                }

                if (ImGui.Button("Create"))
                {
                    if(!string.IsNullOrEmpty(_newProjectPath))
                        CreateProject(_newProjectName, _newProjectPath, _editorContext);
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

        private void DrawAboutModal()
        {
            if (ImGui.BeginPopupModal("AboutDlgKey"))
            {
                ImGui.Text("BYOND 2.0 Editor");
                ImGui.Text("An open-source editor for a new era of 2D games.");
                if (ImGui.Button("OK"))
                {
                    ImGui.CloseCurrentPopup();
                }
                ImGui.EndPopup();
            }
        }

        public static void CreateProject(string projectName, string projectPath, EditorContext editorContext)
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

                editorContext.ProjectRoot = fullProjectPath;
                editorContext.ServerSettings = serverConfig;

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
