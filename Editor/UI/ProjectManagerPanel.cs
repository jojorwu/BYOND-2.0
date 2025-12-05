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
        private readonly LocalizationManager _localizationManager;

        public ProjectManagerPanel(EditorContext editorContext, LocalizationManager localizationManager)
        {
            _editorContext = editorContext;
            _localizationManager = localizationManager;
            _serverBrowserPanel = new ServerBrowserPanel(localizationManager);
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
                var result = dialog.Open(out string[]? outPaths);
                if (result == DialogResult.Okay && outPaths is { Length: > 0 })
                {
                    projectToLoad = outPaths[0];
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
                    foreach (var project in _editorContext.RecentProjects)
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
                    var result = dialog.Open(out string[]? outPaths);
                    if (result == DialogResult.Okay && outPaths is { Length: > 0 })
                    {
                        _newProjectPath = outPaths[0];
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
                _editorContext.AddRecentProject(projectToLoad);
            }

            return projectToLoad;
        }
    }
}
