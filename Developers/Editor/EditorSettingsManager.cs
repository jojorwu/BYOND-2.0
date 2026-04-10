using Shared.Attributes;
using Shared.Services;
using Shared.Config;

namespace Editor;

/// <summary>
/// Manages Editor-specific configuration settings.
/// </summary>
[EngineService]
public class EditorSettingsManager : EngineService
{
    private readonly IConfigurationManager _config;

    public EditorSettingsManager(IConfigurationManager config)
    {
        _config = config;
    }

    public T GetSetting<T>(string key) => _config.GetCVar<T>(key);
    public void SetSetting<T>(string key, T value) => _config.SetCVar(key, value);
}
