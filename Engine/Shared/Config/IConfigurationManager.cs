using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text.Json;

namespace Shared.Config;

public interface IConfigurationManager
{
    T GetCVar<T>(string name);
    void SetCVar<T>(string name, T value);
    void RegisterCVar<T>(string name, T defaultValue, CVarFlags flags = CVarFlags.None, string description = "");
    void Load(string path);
    void Save(string path);
    event Action<string, object> OnCVarChanged;
    IEnumerable<CVarInfo> GetRegisteredCVars();
}

public class CVarInfo
{
    public string Name { get; set; } = "";
    public object Value { get; set; } = null!;
    public object DefaultValue { get; set; } = null!;
    public CVarFlags Flags { get; set; }
    public string Description { get; set; } = "";
    public Type Type { get; set; } = null!;
}

public class ConfigurationManager : IConfigurationManager
{
    private readonly ConcurrentDictionary<string, CVarInfo> _cvars = new();

    public event Action<string, object>? OnCVarChanged;

    public T GetCVar<T>(string name)
    {
        if (_cvars.TryGetValue(name, out var info))
        {
            if (info.Value is JsonElement element)
            {
                var val = element.Deserialize<T>()!;
                info.Value = val; // Cache deserialized value
                return val;
            }
            return (T)info.Value;
        }
        throw new KeyNotFoundException($"CVar '{name}' not found.");
    }

    public void SetCVar<T>(string name, T value)
    {
        if (_cvars.TryGetValue(name, out var info))
        {
            T currentVal;
            if (info.Value is JsonElement element)
            {
                try
                {
                    currentVal = element.Deserialize<T>()!;
                    info.Value = currentVal;
                }
                catch
                {
                    currentVal = default!;
                }
            }
            else
            {
                currentVal = (T)info.Value;
            }

            if (!EqualityComparer<T>.Default.Equals(currentVal, value))
            {
                info.Value = value!;
                OnCVarChanged?.Invoke(name, value!);
            }
        }
        else
        {
            RegisterCVar(name, value!);
        }
    }

    public void RegisterCVar<T>(string name, T defaultValue, CVarFlags flags = CVarFlags.None, string description = "")
    {
        _cvars.TryAdd(name, new CVarInfo
        {
            Name = name,
            Value = defaultValue!,
            DefaultValue = defaultValue!,
            Flags = flags,
            Description = description,
            Type = typeof(T)
        });
    }

    public void Load(string path)
    {
        if (!File.Exists(path)) return;

        try
        {
            var json = File.ReadAllText(path);
            var loaded = JsonSerializer.Deserialize<Dictionary<string, object>>(json);
            if (loaded != null)
            {
                foreach (var kvp in loaded)
                {
                    if (_cvars.TryGetValue(kvp.Key, out var info))
                    {
                        object val = kvp.Value;
                        if (val is JsonElement element && info.Type != typeof(object))
                        {
                            try
                            {
                                val = element.Deserialize(info.Type)!;
                            }
                            catch
                            {
                                // Fallback to element if deserialization fails
                            }
                        }
                        info.Value = val;
                        OnCVarChanged?.Invoke(kvp.Key, val);
                    }
                    else
                    {
                        // Register unknown CVars as objects for later type resolution
                        _cvars.TryAdd(kvp.Key, new CVarInfo { Name = kvp.Key, Value = kvp.Value, Type = typeof(object) });
                    }
                }
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Failed to load config: {ex.Message}");
        }
    }

    public void Save(string path)
    {
        try
        {
            var toSave = _cvars.Values
                .Where(c => (c.Flags & CVarFlags.Archive) != 0)
                .ToDictionary(c => c.Name, c => c.Value);

            var options = new JsonSerializerOptions { WriteIndented = true };
            var json = JsonSerializer.Serialize(toSave, options);
            File.WriteAllText(path, json);
        }
        catch (Exception ex)
        {
             Console.WriteLine($"Failed to save config: {ex.Message}");
        }
    }

    public IEnumerable<CVarInfo> GetRegisteredCVars() => _cvars.Values;
}
