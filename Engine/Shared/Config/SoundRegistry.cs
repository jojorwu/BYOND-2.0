using System.Collections.Concurrent;
using System.Collections.Generic;

namespace Shared.Config;

public record SoundDefinition(string FilePath, float DefaultVolume = 100f, float DefaultPitch = 1f, float DefaultFalloff = 1f);

public interface ISoundRegistry
{
    void RegisterSound(string name, SoundDefinition definition);
    bool TryGetSound(string name, out SoundDefinition definition);
    IEnumerable<string> GetRegisteredNames();
    void LoadFromJson(string filePath);
}

public class SoundRegistry : ISoundRegistry
{
    private readonly ConcurrentDictionary<string, SoundDefinition> _sounds = new();

    public void RegisterSound(string name, SoundDefinition definition)
    {
        _sounds[name.ToLowerInvariant()] = definition;
    }

    public bool TryGetSound(string name, out SoundDefinition definition)
    {
        return _sounds.TryGetValue(name.ToLowerInvariant(), out definition!);
    }

    public IEnumerable<string> GetRegisteredNames() => _sounds.Keys;

    public void LoadFromJson(string filePath)
    {
        if (!System.IO.File.Exists(filePath)) return;

        try
        {
            var json = System.IO.File.ReadAllText(filePath);
            var data = System.Text.Json.JsonSerializer.Deserialize<Dictionary<string, SoundDefinition>>(json);
            if (data != null)
            {
                foreach (var kvp in data)
                {
                    RegisterSound(kvp.Key, kvp.Value);
                }
            }
        }
        catch (System.Exception ex)
        {
            // In a real engine we would log this to a proper logger
            System.Console.WriteLine($"[ERROR] Failed to load sounds from {filePath}: {ex.Message}");
        }
    }
}
