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

