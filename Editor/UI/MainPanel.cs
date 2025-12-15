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
        // We will add the scene-related panels later
        private EditorTab _currentTab = EditorTab.Projects;

        public MainPanel(ProjectsPanel projectsPanel, ServerBrowserPanel serverBrowserPanel, EditorLaunchOptions launchOptions)
        {
            _projectsPanel = projectsPanel;
            _serverBrowserPanel = serverBrowserPanel;

            if (!string.IsNullOrEmpty(launchOptions.InitialPanel))
            {
                if (System.Enum.TryParse<EditorTab>(launchOptions.InitialPanel, true, out var tab))
                {
                    _currentTab = tab;
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

            // This is where we will draw the main content based on the selected tab.
            // For now, it's just a placeholder. We will draw the actual panels in later steps.
            DrawTabBar();

            switch (_currentTab)
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
                    if (ImGui.MenuItem("Projects", "", _currentTab == EditorTab.Projects))
                    {
                        _currentTab = EditorTab.Projects;
                    }
                    if (ImGui.MenuItem("Server Browser", "", _currentTab == EditorTab.ServerBrowser))
                    {
                        _currentTab = EditorTab.ServerBrowser;
                    }
                    if (ImGui.MenuItem("Scene", "", _currentTab == EditorTab.Scene))
                    {
                        _currentTab = EditorTab.Scene;
                    }
                    ImGui.EndMenu();
                }
                ImGui.EndMenuBar();
            }
        }
    }
}
