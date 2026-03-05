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
    void RegisterCVar<T>(string name, T defaultValue, CVarFlags flags = CVarFlags.None, string description = "", string category = "General");
    CVar<T> GetCVarHandle<T>(string name);
    void AddProvider(IConfigProvider provider);
    void RegisterFromAssemblies(params Assembly[] assemblies);
    void LoadAll();
    void SaveAll();
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
    public string Category { get; set; } = "General";
    public Type Type { get; set; } = null!;
    public object? Handle { get; set; }
}

public class ConfigurationManager : IConfigurationManager
{
    private readonly ConcurrentDictionary<string, CVarInfo> _cvars = new();
    private readonly List<IConfigProvider> _providers = new();

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
                if (info.Handle is CVar<T> handle)
                {
                    handle.Value = value!;
                }
                OnCVarChanged?.Invoke(name, value!);
            }
        }
        else
        {
            RegisterCVar(name, value!);
        }
    }

    public void RegisterCVar<T>(string name, T defaultValue, CVarFlags flags = CVarFlags.None, string description = "", string category = "General")
    {
        if (_cvars.TryGetValue(name, out var info))
        {
            info.DefaultValue = defaultValue!;
            info.Flags = flags;
            info.Description = description;
            info.Category = category;
            info.Type = typeof(T);

            // Try to resolve existing value if it was loaded as a generic object or JsonElement
            if (info.Value is JsonElement element)
            {
                try
                {
                    info.Value = element.Deserialize<T>()!;
                }
                catch { /* Stick with element if fail */ }
            }
        }
        else
        {
            _cvars.TryAdd(name, new CVarInfo
            {
                Name = name,
                Value = defaultValue!,
                DefaultValue = defaultValue!,
                Flags = flags,
                Description = description,
                Category = category,
                Type = typeof(T)
            });
        }
    }

    public CVar<T> GetCVarHandle<T>(string name)
    {
        if (_cvars.TryGetValue(name, out var info))
        {
            if (info.Handle == null)
            {
                var handle = new CVar<T>(name, GetCVar<T>(name));
                info.Handle = handle;
                return handle;
            }
            return (CVar<T>)info.Handle;
        }
        throw new KeyNotFoundException($"CVar '{name}' not found.");
    }

    public void RegisterFromAssemblies(params Assembly[] assemblies)
    {
        foreach (var assembly in assemblies)
        {
            foreach (var type in assembly.GetTypes())
            {
                foreach (var field in type.GetFields(BindingFlags.Public | BindingFlags.Static | BindingFlags.NonPublic))
                {
                    var attr = field.GetCustomAttribute<CVarAttribute>();
                    if (attr != null)
                    {
                        var defaultValue = field.GetValue(null);
                        var registerMethod = typeof(ConfigurationManager).GetMethod(nameof(RegisterCVar))!.MakeGenericMethod(field.FieldType);
                        registerMethod.Invoke(this, new[] { attr.Name, defaultValue, attr.Flags, attr.Description, attr.Category });
                    }
                }
            }
        }
    }

    public void AddProvider(IConfigProvider provider) => _providers.Add(provider);

    public void LoadAll()
    {
        var settings = new Dictionary<string, object>();
        foreach (var provider in _providers)
        {
            provider.Load(settings);
        }

        foreach (var kvp in settings)
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
                    catch { }
                }
                info.Value = val;
                OnCVarChanged?.Invoke(kvp.Key, val);
            }
            else
            {
                _cvars.TryAdd(kvp.Key, new CVarInfo { Name = kvp.Key, Value = kvp.Value, Type = typeof(object) });
            }
        }
    }

    public void SaveAll()
    {
        var settings = _cvars.Values
            .Where(c => (c.Flags & CVarFlags.Archive) != 0)
            .ToDictionary(c => c.Name, c => c.Value);

        foreach (var provider in _providers.Where(p => p.CanSave))
        {
            provider.Save(settings);
        }
    }

    // Keep Load/Save for compatibility or remove if not needed.
    // Let's refactor them to use JsonConfigProvider temporarily.
    public void Load(string path)
    {
        var provider = new JsonConfigProvider(path);
        var settings = new Dictionary<string, object>();
        provider.Load(settings);
        foreach(var kvp in settings) {
             if (_cvars.TryGetValue(kvp.Key, out var info)) {
                 info.Value = kvp.Value;
                 OnCVarChanged?.Invoke(kvp.Key, kvp.Value);
             } else {
                 _cvars.TryAdd(kvp.Key, new CVarInfo { Name = kvp.Key, Value = kvp.Value, Type = typeof(object) });
             }
        }
    }

    public void Save(string path)
    {
        var provider = new JsonConfigProvider(path);
        var settings = _cvars.Values
            .Where(c => (c.Flags & CVarFlags.Archive) != 0)
            .ToDictionary(c => c.Name, c => c.Value);
        provider.Save(settings);
    }

    public IEnumerable<CVarInfo> GetRegisteredCVars() => _cvars.Values;
}
