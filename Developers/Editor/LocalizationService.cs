using System.Collections.Generic;
using Shared.Attributes;
using Shared.Services;

namespace Editor;

/// <summary>
/// Provides multi-language support for the Editor.
/// </summary>
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
            { "Button.Save", "Save" },
            { "Panel.Hierarchy.Title", "Hierarchy" },
            { "Panel.Inspector.Title", "Inspector" },
            { "Panel.AssetBrowser.Title", "Asset Browser" },
            { "Panel.Viewport.Title", "Viewport" }
        };
        _translations["ru"] = new Dictionary<string, string> {
            { "Menu.File", "Файл" },
            { "Menu.Edit", "Правка" },
            { "Menu.View", "Вид" },
            { "Button.Save", "Сохранить" },
            { "Panel.Hierarchy.Title", "Иерархия" },
            { "Panel.Inspector.Title", "Инспектор" },
            { "Panel.AssetBrowser.Title", "Браузер ассетов" },
            { "Panel.Viewport.Title", "Вид" }
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
