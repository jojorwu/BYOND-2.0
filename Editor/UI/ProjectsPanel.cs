using ImGuiNET;
using NativeFileDialogNET;
using Shared;
using System;
using System.IO;
using System.Numerics;
using System.Threading.Tasks;

namespace Editor.UI
{
    public class ProjectsPanel
    {
        private readonly EditorContext _editorContext;
        private readonly IProjectManager _projectManager;
        private readonly LocalizationManager _localizationManager;
        private readonly Editor _editor; // To call LoadProject

        private string _newProjectName = "MyNewProject";
        private string _newProjectPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments), "BYOND2.0Projects");

        public ProjectsPanel(EditorContext editorContext, IProjectManager projectManager, LocalizationManager localizationManager, Editor editor)
        {
            _editorContext = editorContext;
            _projectManager = projectManager;
            _localizationManager = localizationManager;
            _editor = editor;
        }

        public void Draw()
        {
            var viewport = ImGui.GetMainViewport();
            ImGui.SetNextWindowPos(viewport.Pos + new Vector2(0, 30)); // Move down to avoid main menu bar
            ImGui.SetNextWindowSize(viewport.Size - new Vector2(0, 30));

            if(ImGui.Begin("Projects", ImGuiWindowFlags.NoTitleBar | ImGuiWindowFlags.NoResize | ImGuiWindowFlags.NoMove))
            {
                ImGui.Text("Recent Projects");
                ImGui.Separator();

                if (ImGui.BeginChild("RecentProjectsList", new Vector2(ImGui.GetContentRegionAvail().X * 0.4f, 0)))
                {
                    foreach (var project in _editorContext.RecentProjects)
                    {
                        if (ImGui.Selectable(project))
                        {
                           _editor.LoadProject(project);
                           _editorContext.AddRecentProject(project);
                        }
                    }
                    ImGui.EndChild();
                }

                ImGui.SameLine();

                if (ImGui.BeginChild("ProjectActions", Vector2.Zero))
                {
                    ImGui.Text("Create New Project");
                    ImGui.Separator();
                    ImGui.InputText("Project Name", ref _newProjectName, 256);
                    ImGui.InputText("Path", ref _newProjectPath, 256);
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

                    if (ImGui.Button("Create & Open"))
                    {
                        if (!string.IsNullOrEmpty(_newProjectPath) && !string.IsNullOrEmpty(_newProjectName))
                        {
                            _ = CreateAndLoadProjectAsync(_newProjectName, _newProjectPath);
                        }
                    }
                    ImGui.EndChild();
                }

                ImGui.End();
            }
        }

        private async Task CreateAndLoadProjectAsync(string projectName, string projectPath)
        {
            var success = await _projectManager.CreateProjectAsync(projectName, projectPath);
            if (success)
            {
                var fullPath = Path.Combine(projectPath, projectName);
                _editor.LoadProject(fullPath);
                _editorContext.AddRecentProject(fullPath);
            }
            // TODO: Add error handling feedback to the user
        }
    }
}
