using ImGuiNET;
using Shared;

namespace Editor.UI
{
    public class SettingsPanel : IUiPanel
    {
        public string Name => "Settings";
        public bool IsOpen { get; set; } = false;

        private readonly IEditorSettingsManager _settingsManager;

        public SettingsPanel(IEditorSettingsManager settingsManager)
        {
            _settingsManager = settingsManager;
        }

        public void Draw()
        {
            if (!IsOpen)
                return;

            bool isOpen = IsOpen;
            if (ImGui.Begin(Name, ref isOpen))
            {
                var settings = _settingsManager.Settings;

                if (ImGui.BeginTabBar("SettingsTabs"))
                {
                    if (ImGui.BeginTabItem("General"))
                    {
                        string serverPath = settings.ServerExecutablePath;
                        string clientPath = settings.ClientExecutablePath;
                        if (ImGui.InputText("Server Executable", ref serverPath, 260)) settings.ServerExecutablePath = serverPath;
                        if (ImGui.InputText("Client Executable", ref clientPath, 260)) settings.ClientExecutablePath = clientPath;

                        bool useDarkTheme = settings.UseDarkTheme;
                        if (ImGui.Checkbox("Use Dark Theme", ref useDarkTheme)) settings.UseDarkTheme = useDarkTheme;

                        int fontSize = settings.FontSize;
                        if (ImGui.InputInt("Font Size", ref fontSize)) settings.FontSize = fontSize;

                        bool autoSave = settings.AutoSave;
                        if (ImGui.Checkbox("Auto Save", ref autoSave)) settings.AutoSave = autoSave;

                        if (settings.AutoSave)
                        {
                            int interval = settings.AutoSaveIntervalMinutes;
                            if (ImGui.InputInt("Auto Save Interval (min)", ref interval)) settings.AutoSaveIntervalMinutes = interval;
                        }

                        ImGui.EndTabItem();
                    }

                    if (ImGui.BeginTabItem("Viewport"))
                    {
                        bool showGrid = settings.ShowGrid;
                        if (ImGui.Checkbox("Show Grid", ref showGrid)) settings.ShowGrid = showGrid;

                        int gridSize = settings.GridSize;
                        if (ImGui.InputInt("Grid Size", ref gridSize)) settings.GridSize = gridSize;

                        var gridColor = settings.GridColor;
                        if (ImGui.ColorEdit4("Grid Color", ref gridColor)) settings.GridColor = gridColor;

                        bool snapToGrid = settings.SnapToGrid;
                        if (ImGui.Checkbox("Snap To Grid", ref snapToGrid)) settings.SnapToGrid = snapToGrid;

                        ImGui.EndTabItem();
                    }

                    ImGui.EndTabBar();
                }

                ImGui.Separator();
                if (ImGui.Button("Save"))
                {
                    _settingsManager.Save();
                }
                ImGui.SameLine();
                if (ImGui.Button("Close"))
                {
                    isOpen = false;
                }

                ImGui.End();
            }
            IsOpen = isOpen;
        }
    }
}
