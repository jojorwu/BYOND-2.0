using ImGuiNET;
using NativeFileDialogNET;

using System.Collections.Generic;
using System.IO;

namespace Editor.UI
{
    public class ProjectManagerPanel
    {
        private string _newProjectName = "NewProject";
        private string _newProjectPath = "";
        private readonly EditorContext _editorContext;
        private readonly ServerBrowserPanel _serverBrowserPanel;
        private readonly List<string> _recentProjects = new List<string>();
        private readonly LocalizationManager _localizationManager;

        public ProjectManagerPanel(EditorContext editorContext, LocalizationManager localizationManager)
        {
            _editorContext = editorContext;
            _localizationManager = localizationManager;
            _serverBrowserPanel = new ServerBrowserPanel(localizationManager);
            LoadRecentProjects();
        }

        public string Draw()
        {
            string projectToLoad = null;

            ImGui.Begin(_localizationManager.GetString("Project Manager"));

            if (ImGui.Button(_localizationManager.GetString("New Project")))
            {
                ImGui.OpenPopup("NewProjectDlgKey");
            }

            ImGui.SameLine();

            if (ImGui.Button(_localizationManager.GetString("Open Project")))
            {
                using var dialog = new NativeFileDialog().SelectFolder();
                if (dialog.ShowDialog() == NativeFileDialog.Result.Okay)
                {
                    projectToLoad = dialog.Path;
                }
            }

            ImGui.SameLine();

            if (ImGui.Button(_localizationManager.GetString("Import Project")))
            {
                ImGui.OpenPopup("ImportProjectDlgKey");
            }

            if (ImGui.BeginTabBar("ProjectManagerTabs"))
            {
                if (ImGui.BeginTabItem(_localizationManager.GetString("Projects")))
                {
                    foreach (var project in _recentProjects)
                    {
                        if (ImGui.Selectable(project))
                        {
                            projectToLoad = project;
                        }
                    }
                    ImGui.EndTabItem();
                }
                _serverBrowserPanel.Draw();
                ImGui.EndTabBar();
            }

            if (ImGui.BeginPopupModal("NewProjectDlgKey"))
            {
                ImGui.InputText(_localizationManager.GetString("Project Name"), ref _newProjectName, 256);
                ImGui.InputText(_localizationManager.GetString("Project Path"), ref _newProjectPath, 256, ImGuiInputTextFlags.ReadOnly);
                ImGui.SameLine();
                if (ImGui.Button("..."))
                {
                    using var dialog = new NativeFileDialog().SelectFolder();
                    if (dialog.ShowDialog() == NativeFileDialog.Result.Okay)
                    {
                        _newProjectPath = dialog.Path;
                    }
                }

                if (ImGui.Button(_localizationManager.GetString("Create")))
                {
                    MenuBarPanel.CreateProject(_newProjectName, _newProjectPath, _editorContext);
                    projectToLoad = System.IO.Path.Combine(_newProjectPath, _newProjectName);
                    ImGui.CloseCurrentPopup();
                }
                ImGui.SameLine();
                if (ImGui.Button(_localizationManager.GetString("Cancel")))
                {
                    ImGui.CloseCurrentPopup();
                }
                ImGui.EndPopup();
            }

            ImGui.End();

            if (projectToLoad != null)
            {
                AddRecentProject(projectToLoad);
            }

            return projectToLoad;
        }

        private void LoadRecentProjects()
        {
            var path = Path.Combine(System.Environment.GetFolderPath(System.Environment.SpecialFolder.ApplicationData), "BYOND2.0Editor", "recent_projects.txt");
            if (File.Exists(path))
            {
                _recentProjects.AddRange(File.ReadAllLines(path));
            }
        }

        private void AddRecentProject(string path)
        {
            if (!_recentProjects.Contains(path))
            {
                _recentProjects.Insert(0, path);
                if (_recentProjects.Count > 10)
                {
                    _recentProjects.RemoveAt(_recentProjects.Count - 1);
                }
                SaveRecentProjects();
            }
        }

        private void SaveRecentProjects()
        {
            var dir = Path.Combine(System.Environment.GetFolderPath(System.Environment.SpecialFolder.ApplicationData), "BYOND2.0Editor");
            Directory.CreateDirectory(dir);
            var path = Path.Combine(dir, "recent_projects.txt");
            File.WriteAllLines(path, _recentProjects);
        }
    }
}
