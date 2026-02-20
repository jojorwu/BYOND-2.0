namespace Shared.Config;

public interface IEngineConfig
{
    T Get<T>(string key, T defaultValue = default!);
    void Set<T>(string key, T value);
    bool Has(string key);
}

public class EngineConfig : IEngineConfig
{
    private readonly System.Collections.Concurrent.ConcurrentDictionary<string, object> _settings = new();

    public T Get<T>(string key, T defaultValue = default!)
    {
        if (_settings.TryGetValue(key, out var value))
        {
            return (T)value;
        }
        return defaultValue;
    }

    public void Set<T>(string key, T value)
    {
        _settings[key] = value!;
    }

    public bool Has(string key)
    {
        return _settings.ContainsKey(key);
    }
}
