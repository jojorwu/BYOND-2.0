using Shared.Models;
using Shared.Interfaces;
using Shared.Enums;
using Shared.Operations;
using Shared.Services;
using ImGuiNET;
using System.Numerics;

namespace Editor.UI
{
    public enum EditorTab
    {
        Projects,
        ServerBrowser,
        Scene
    }

    public class MainPanel
    {
        private readonly ProjectsPanel _projectsPanel;
        private readonly ServerBrowserPanel _serverBrowserPanel;
        private readonly IUIService _uiService;

        public MainPanel(ProjectsPanel projectsPanel, ServerBrowserPanel serverBrowserPanel, EditorLaunchOptions launchOptions, IUIService uiService)
        {
            _projectsPanel = projectsPanel;
            _serverBrowserPanel = serverBrowserPanel;
            _uiService = uiService;

            if (!string.IsNullOrEmpty(launchOptions.InitialPanel))
            {
                if (System.Enum.TryParse<EditorTab>(launchOptions.InitialPanel, true, out var tab))
                {
                    _uiService.SetActiveTab(tab);
                }
            }
        }

        public void Draw()
        {
            // Set up the main dock space
            var viewport = ImGui.GetMainViewport();
            ImGui.SetNextWindowPos(viewport.Pos);
            ImGui.SetNextWindowSize(viewport.Size);
            ImGui.SetNextWindowViewport(viewport.ID);
            ImGui.PushStyleVar(ImGuiStyleVar.WindowRounding, 0.0f);
            ImGui.PushStyleVar(ImGuiStyleVar.WindowBorderSize, 0.0f);
            ImGui.PushStyleVar(ImGuiStyleVar.WindowPadding, Vector2.Zero);

            ImGuiWindowFlags windowFlags = ImGuiWindowFlags.MenuBar | ImGuiWindowFlags.NoDocking;
            windowFlags |= ImGuiWindowFlags.NoTitleBar | ImGuiWindowFlags.NoCollapse | ImGuiWindowFlags.NoResize | ImGuiWindowFlags.NoMove;
            windowFlags |= ImGuiWindowFlags.NoBringToFrontOnFocus | ImGuiWindowFlags.NoNavFocus;

            ImGui.Begin("MainDockSpace", windowFlags);
            ImGui.PopStyleVar(3);

            var dockspaceId = ImGui.GetID("MyDockSpace");
            ImGui.DockSpace(dockspaceId, Vector2.Zero, ImGuiDockNodeFlags.None);

            DrawTabBar();

            switch (_uiService.GetActiveTab())
            {
                case EditorTab.Projects:
                    _projectsPanel.Draw();
                    break;
                case EditorTab.ServerBrowser:
                    _serverBrowserPanel.Draw();
                    break;
                case EditorTab.Scene:
                     // Draw scene related panels
                    break;
            }

            ImGui.End();
        }

        private void DrawTabBar()
        {
            if (ImGui.BeginMenuBar())
            {
                if (ImGui.BeginMenu("View")) // Using a menu for tabs for now
                {
                    if (ImGui.MenuItem("Projects", "", _uiService.GetActiveTab() == EditorTab.Projects))
                    {
                        _uiService.SetActiveTab(EditorTab.Projects);
                    }
                    if (ImGui.MenuItem("Server Browser", "", _uiService.GetActiveTab() == EditorTab.ServerBrowser))
                    {
                        _uiService.SetActiveTab(EditorTab.ServerBrowser);
                    }
                    if (ImGui.MenuItem("Scene", "", _uiService.GetActiveTab() == EditorTab.Scene))
                    {
                        _uiService.SetActiveTab(EditorTab.Scene);
                    }
                    ImGui.EndMenu();
                }
                ImGui.EndMenuBar();
            }
        }
    }
}
