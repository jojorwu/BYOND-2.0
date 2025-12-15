using ImGuiNET;

namespace Editor.UI
{
    public class SettingsPanel : IUiPanel
    {
        private readonly LocalizationManager _localizationManager;
        private int _selectedLanguage = 0;
        private readonly string[] _languages = { "en", "ru" };

        public SettingsPanel(LocalizationManager localizationManager)
        {
            _localizationManager = localizationManager;
        }

        public void Draw()
        {
            ImGui.Begin("Settings");
            if (ImGui.Combo("Language", ref _selectedLanguage, _languages, _languages.Length))
            {
                _localizationManager.LoadLanguage(_languages[_selectedLanguage]);
            }
            ImGui.End();
        }
    }
}
