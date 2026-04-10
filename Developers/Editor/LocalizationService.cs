using System.Collections.Generic;
using Shared.Attributes;
using Shared.Services;

namespace Editor
{
    [EngineService]
    public class LocalizationService : EngineService
    {
        private string _currentLanguage = "en";
        private readonly Dictionary<string, Dictionary<string, string>> _translations = new();

        public LocalizationService()
        {
            _translations["en"] = new Dictionary<string, string> {
                { "Menu.File", "File" },
                { "Menu.Edit", "Edit" },
                { "Menu.View", "View" },
                { "Button.Save", "Save" }
            };
            _translations["ru"] = new Dictionary<string, string> {
                { "Menu.File", "Файл" },
                { "Menu.Edit", "Правка" },
                { "Menu.View", "Вид" },
                { "Button.Save", "Сохранить" }
            };
        }

        public string GetString(string key)
        {
            if (_translations.TryGetValue(_currentLanguage, out var lang) && lang.TryGetValue(key, out var val))
                return val;
            return key;
        }

        public void SetLanguage(string lang) => _currentLanguage = lang;
    }
}
