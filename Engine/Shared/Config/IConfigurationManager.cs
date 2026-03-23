using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text.Json;

using Shared.Services;

namespace Shared.Config;

public interface IConfigurationManager
{
    T GetCVar<T>(string name);
    void SetCVar<T>(string name, T value);
    void RegisterCVar<T>(string name, T defaultValue, CVarFlags flags = CVarFlags.None, string description = "", string category = "General", object? minValue = null, object? maxValue = null, Func<T, bool>? validator = null);
    CVar<T> GetCVarHandle<T>(string name);
    void AddProvider(IConfigProvider provider);
    void RegisterFromAssemblies(params Assembly[] assemblies);
    void LoadAll();
    void SaveAll();
    event Action<string, object> OnCVarChanged;
    IEnumerable<CVarInfo> GetRegisteredCVars();
}

public record CVarDef<T>(string Name, T DefaultValue, CVarFlags Flags = CVarFlags.None, string Description = "", string Category = "General", object? MinValue = null, object? MaxValue = null)
{
    public static implicit operator string(CVarDef<T> def) => def.Name;
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
    public object? MinValue { get; set; }
    public object? MaxValue { get; set; }
    public bool IsLocked { get; set; }
    public object? Validator { get; set; }
}

public class ConfigurationManager : EngineService, IConfigurationManager
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
            // Simple validation for comparable types
            if (value is IComparable comparable)
            {
                if (info.MinValue is IComparable min && comparable.CompareTo(min) < 0) value = (T)info.MinValue;
                if (info.MaxValue is IComparable max && comparable.CompareTo(max) > 0) value = (T)info.MaxValue;
            }

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

    public void RegisterCVar<T>(string name, T defaultValue, CVarFlags flags = CVarFlags.None, string description = "", string category = "General", object? minValue = null, object? maxValue = null, Func<T, bool>? validator = null)
    {
        if (_cvars.TryGetValue(name, out var info))
        {
            info.DefaultValue = defaultValue!;
            info.Flags = flags;
            info.Description = description;
            info.Category = category;
            info.Type = typeof(T);
            info.MinValue = minValue;
            info.MaxValue = maxValue;
            info.Validator = validator;

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
                Type = typeof(T),
                MinValue = minValue,
                MaxValue = maxValue,
                Validator = validator
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
                    // Check for CVarDef<T>
                    if (field.FieldType.IsGenericType && field.FieldType.GetGenericTypeDefinition() == typeof(CVarDef<>))
                    {
                        var def = field.GetValue(null);
                        if (def == null) continue;

                        var typeT = field.FieldType.GetGenericArguments()[0];

                        var name = (string)field.FieldType.GetProperty("Name")!.GetValue(def)!;
                        var defaultValue = field.FieldType.GetProperty("DefaultValue")!.GetValue(def);
                        var flags = (CVarFlags)field.FieldType.GetProperty("Flags")!.GetValue(def)!;
                        var description = (string)field.FieldType.GetProperty("Description")!.GetValue(def)!;
                        var category = (string)field.FieldType.GetProperty("Category")!.GetValue(def)!;
                        var minValue = field.FieldType.GetProperty("MinValue")!.GetValue(def);
                        var maxValue = field.FieldType.GetProperty("MaxValue")!.GetValue(def);

                        var registerMethod = typeof(ConfigurationManager).GetMethods().First(m => m.Name == nameof(RegisterCVar) && m.IsGenericMethod)!.MakeGenericMethod(typeT);
                        registerMethod.Invoke(this, new[] { name, defaultValue, flags, description, category, minValue, maxValue, null });
                    }
                }
            }
        }
    }

    public void AddProvider(IConfigProvider provider)
    {
        _providers.Add(provider);
        _providers.Sort((a, b) => a.Priority.CompareTo(b.Priority));
    }

    public void SetCVarDirect(string name, object value)
    {
        if (_cvars.TryGetValue(name, out var info))
        {
            object? convertedValue = value;
            try
            {
                if (value is JsonElement element)
                {
                    convertedValue = element.Deserialize(info.Type);
                }
                else if (value is string str && info.Type != typeof(string))
                {
                    if (info.Type == typeof(bool)) convertedValue = bool.Parse(str);
                    else if (info.Type == typeof(int)) convertedValue = int.Parse(str);
                    else if (info.Type == typeof(long)) convertedValue = long.Parse(str);
                    else if (info.Type == typeof(float)) convertedValue = float.Parse(str, System.Globalization.CultureInfo.InvariantCulture);
                    else if (info.Type == typeof(double)) convertedValue = double.Parse(str, System.Globalization.CultureInfo.InvariantCulture);
                    else convertedValue = Convert.ChangeType(str, info.Type);
                }
                else if (value != null && !info.Type.IsAssignableFrom(value.GetType()))
                {
                    convertedValue = Convert.ChangeType(value, info.Type);
                }
            }
            catch (Exception ex)
            {
                throw new InvalidOperationException($"Failed to convert value '{value}' to type {info.Type} for CVar '{name}'", ex);
            }

            var method = typeof(ConfigurationManager).GetMethods().First(m => m.Name == nameof(SetCVar) && m.IsGenericMethod)!.MakeGenericMethod(info.Type);
            method.Invoke(this, new[] { name, convertedValue });
        }
    }

    public void LoadAll()
    {
        var settings = new Dictionary<string, object>();
        var lockedKeys = new HashSet<string>();

        foreach (var provider in _providers.OrderBy(p => p.Priority))
        {
            var providerSettings = new Dictionary<string, object>();
            provider.Load(providerSettings);
            foreach (var kvp in providerSettings)
            {
                settings[kvp.Key] = kvp.Value;
                if (!provider.CanSave) lockedKeys.Add(kvp.Key);
                else lockedKeys.Remove(kvp.Key);
            }
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
                info.IsLocked = lockedKeys.Contains(kvp.Key);
                OnCVarChanged?.Invoke(kvp.Key, val);
            }
            else
            {
                _cvars.TryAdd(kvp.Key, new CVarInfo
                {
                    Name = kvp.Key,
                    Value = kvp.Value,
                    Type = typeof(object),
                    IsLocked = lockedKeys.Contains(kvp.Key)
                });
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
