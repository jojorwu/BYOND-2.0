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
        private readonly ViewportPanel _viewportPanel;
        private readonly ToolbarPanel _toolbarPanel;
        private readonly InspectorPanel _inspectorPanel;
        private readonly ObjectBrowserPanel _objectBrowserPanel;
        private readonly AssetBrowserPanel _assetBrowserPanel;
        private readonly SceneHierarchyPanel _sceneHierarchyPanel;
        private readonly EditorContext _editorContext;
        private readonly IUIService _uiService;

        public MainPanel(
            ProjectsPanel projectsPanel,
            ServerBrowserPanel serverBrowserPanel,
            ViewportPanel viewportPanel,
            ToolbarPanel toolbarPanel,
            InspectorPanel inspectorPanel,
            ObjectBrowserPanel objectBrowserPanel,
            AssetBrowserPanel assetBrowserPanel,
            SceneHierarchyPanel sceneHierarchyPanel,
            EditorContext editorContext,
            EditorLaunchOptions launchOptions,
            IUIService uiService)
        {
            _projectsPanel = projectsPanel;
            _serverBrowserPanel = serverBrowserPanel;
            _viewportPanel = viewportPanel;
            _toolbarPanel = toolbarPanel;
            _inspectorPanel = inspectorPanel;
            _objectBrowserPanel = objectBrowserPanel;
            _assetBrowserPanel = assetBrowserPanel;
            _sceneHierarchyPanel = sceneHierarchyPanel;
            _editorContext = editorContext;
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
            DrawStatusBar();

            switch (_uiService.GetActiveTab())
            {
                case EditorTab.Projects:
                    _projectsPanel.Draw();
                    break;
                case EditorTab.ServerBrowser:
                    _serverBrowserPanel.Draw();
                    break;
                case EditorTab.Scene:
                    _toolbarPanel.Draw();
                    _sceneHierarchyPanel.Draw();
                    _inspectorPanel.Draw();
                    _objectBrowserPanel.Draw();
                    _assetBrowserPanel.Draw();

                    var activeScene = _editorContext.GetActiveScene();
                    if (activeScene != null)
                    {
                        _viewportPanel.Draw(activeScene);
                    }
                    else
                    {
                        ImGui.Begin("Viewport");
                        ImGui.Text("No active scene.");
                        ImGui.End();
                    }
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

        private void DrawStatusBar()
        {
            var viewport = ImGui.GetMainViewport();
            ImGui.SetNextWindowPos(new Vector2(viewport.Pos.X, viewport.Pos.Y + viewport.Size.Y - 25));
            ImGui.SetNextWindowSize(new Vector2(viewport.Size.X, 25));
            ImGuiWindowFlags flags = ImGuiWindowFlags.NoTitleBar | ImGuiWindowFlags.NoResize | ImGuiWindowFlags.NoMove | ImGuiWindowFlags.NoScrollbar | ImGuiWindowFlags.NoSavedSettings | ImGuiWindowFlags.NoInputs;

            ImGui.PushStyleColor(ImGuiCol.WindowBg, new Vector4(0.1f, 0.1f, 0.1f, 1.0f));
            if (ImGui.Begin("StatusBar", flags))
            {
                var scene = _editorContext.GetActiveScene();
                string sceneInfo = scene != null ? $"Scene: {scene.FilePath}" : "No Scene Active";

                ImGui.Text($"{sceneInfo}");
                ImGui.SameLine(ImGui.GetWindowWidth() - 150);
                ImGui.Text($"FPS: {ImGui.GetIO().Framerate:F1}");

                ImGui.End();
            }
            ImGui.PopStyleColor();
        }
    }
}
