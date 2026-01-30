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

                string serverPath = settings.ServerExecutablePath;
                string clientPath = settings.ClientExecutablePath;

                if (ImGui.InputText("Server Executable", ref serverPath, 260)) settings.ServerExecutablePath = serverPath;
                if (ImGui.InputText("Client Executable", ref clientPath, 260)) settings.ClientExecutablePath = clientPath;

                if (ImGui.Button("Save"))
                {
                    _settingsManager.Save();
                }

                ImGui.End();
            }
            IsOpen = isOpen;
        }
    }
}
