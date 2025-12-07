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

        public ProjectManagerPanel(EditorContext editorContext, LocalizationManager localizationManager, ServerBrowserPanel serverBrowserPanel)
        {
            _editorContext = editorContext;
            _localizationManager = localizationManager;
            _serverBrowserPanel = serverBrowserPanel;
        }

        public string? Draw()
        {
            string? projectToLoad = null;

            ImGui.Begin(_localizationManager.GetString("Project Manager"));

            if (ImGui.Button(_localizationManager.GetString("New Project")))
            {
                ImGui.OpenPopup("NewProjectDlgKey");
            }

            ImGui.SameLine();

            if (ImGui.Button(_localizationManager.GetString("Open Project")))
            {
                using var dialog = new NativeFileDialog().SelectFolder();
                DialogResult result = dialog.Open(out string? path);
                if (result == DialogResult.Okay && path != null)
                {
                    projectToLoad = path;
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
                    DialogResult result = dialog.Open(out string? path);
                    if (result == DialogResult.Okay && path != null)
                    {
                        _newProjectPath = path;
                    }
                }

                if (ImGui.Button(_localizationManager.GetString("Create")))
                {
                    if (!string.IsNullOrEmpty(_newProjectPath))
                    {
                        MenuBarPanel.CreateProject(_newProjectName, _newProjectPath, _editorContext);
                        projectToLoad = System.IO.Path.Combine(_newProjectPath, _newProjectName);
                    }
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
