using ImGuiNET;

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

            if (ImGui.Begin(Name, ref IsOpen))
            {
                var settings = _settingsManager.Settings;

                ImGui.InputText("Server Executable", ref settings.ServerExecutablePath, 260);
                ImGui.InputText("Client Executable", ref settings.ClientExecutablePath, 260);

                if (ImGui.Button("Save"))
                {
                    _settingsManager.Save();
                }

                ImGui.End();
            }
        }
    }
}
