
using ImGuiNET;
using System;
using System.Numerics;
using Core;

using System.IO;
using System.Linq;

namespace Editor.UI
{
    public enum MainMenuAction
    {
        None,
        LoadProject,
        NewProject,
        Settings,
        Exit
    }

    public class MainMenuPanel
    {
        private bool _showNewProjectDialog = false;
        private string _newProjectName = string.Empty;
        private string[] _existingProjects = Array.Empty<string>();
        private const string ProjectsDir = "Projects";

        public string SelectedProject { get; private set; } = string.Empty;

        public MainMenuPanel()
        {
            RefreshProjectList();
        }

        public MainMenuAction Draw()
        {
            var action = MainMenuAction.None;

            var viewport = ImGui.GetMainViewport();
            ImGui.SetNextWindowPos(viewport.GetCenter(), ImGuiCond.Always, new Vector2(0.5f, 0.5f));
            ImGui.Begin("Main Menu", ImGuiWindowFlags.NoDocking | ImGuiWindowFlags.NoTitleBar | ImGuiWindowFlags.NoResize | ImGuiWindowFlags.AlwaysAutoResize);

            ImGui.Text("BYOND 2.0");
            ImGui.Separator();

            if (ImGui.Button("New Project", new Vector2(120, 0)))
            {
                _showNewProjectDialog = true;
                _newProjectName = string.Empty;
            }

            ImGui.Text("Existing Projects:");
            foreach (var project in _existingProjects)
            {
                if (ImGui.Selectable(project))
                {
                    SelectedProject = Path.Combine(ProjectsDir, project);
                    action = MainMenuAction.LoadProject;
                }
            }

            ImGui.Separator();

            if (ImGui.Button("Settings", new Vector2(120, 0)))
            {
                action = MainMenuAction.Settings;
            }

            if (ImGui.Button("Exit", new Vector2(120, 0)))
            {
                action = MainMenuAction.Exit;
            }

            ImGui.End();

            if (_showNewProjectDialog)
            {
                if (DrawNewProjectDialog())
                {
                    SelectedProject = Path.Combine(ProjectsDir, _newProjectName);
                    action = MainMenuAction.NewProject;
                }
            }

            return action;
        }

        private bool DrawNewProjectDialog()
        {
            bool createClicked = false;
            ImGui.SetNextWindowSize(new Vector2(300, 100));
            ImGui.Begin("Create New Project", ref _showNewProjectDialog, ImGuiWindowFlags.NoResize | ImGuiWindowFlags.NoCollapse);

            ImGui.InputText("Project Name", ref _newProjectName, 64);

            ImGui.Spacing();
            if (ImGui.Button("Create", new Vector2(120, 0)))
            {
                if (!string.IsNullOrWhiteSpace(_newProjectName))
                {
                    createClicked = true;
                    _showNewProjectDialog = false;
                    RefreshProjectList();
                }
            }
            ImGui.SameLine();
            if (ImGui.Button("Cancel", new Vector2(120, 0)))
            {
                _showNewProjectDialog = false;
            }
            ImGui.End();
            return createClicked;
        }

        private void RefreshProjectList()
        {
            if (!Directory.Exists(ProjectsDir))
            {
                Directory.CreateDirectory(ProjectsDir);
            }
            _existingProjects = Directory.GetDirectories(ProjectsDir).Select(Path.GetFileName).ToArray()!;
        }
    }
}
