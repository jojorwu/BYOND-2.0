using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;

namespace Shared.Config;

public interface IEngineConfig
{
    T Get<T>(string key, T defaultValue = default!);
    void Set<T>(string key, T value);
    bool Has(string key);
}

public class EngineConfig : IEngineConfig
{
    private readonly IConfigurationManager _manager;

    public EngineConfig(IConfigurationManager manager)
    {
        _manager = manager;
    }

    public T Get<T>(string key, T defaultValue = default!)
    {
        try
        {
            return _manager.GetCVar<T>(key);
        }
        catch (KeyNotFoundException)
        {
            _manager.RegisterCVar(key, defaultValue);
            return defaultValue;
        }
    }

    public void Set<T>(string key, T value)
    {
        _manager.SetCVar(key, value);
    }

    public bool Has(string key)
    {
        return _manager.GetRegisteredCVars().Any(c => c.Name == key);
    }
}
