using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;

namespace Shared.Config;

public class JsonConfigProvider : IConfigProvider
{
    private readonly string _path;
    public string Name => $"Json({_path})";
    public bool CanSave => true;

    public JsonConfigProvider(string path)
    {
        _path = path;
    }

    public void Load(IDictionary<string, object> settings)
    {
        if (!File.Exists(_path)) return;

        try
        {
            var json = File.ReadAllText(_path);
            var loaded = JsonSerializer.Deserialize<Dictionary<string, object>>(json);
            if (loaded != null)
            {
                foreach (var kvp in loaded)
                {
                    settings[kvp.Key] = kvp.Value;
                }
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Failed to load config from {_path}: {ex.Message}");
        }
    }

    public void Save(IDictionary<string, object> settings)
    {
        try
        {
            var options = new JsonSerializerOptions { WriteIndented = true };
            var json = JsonSerializer.Serialize(settings, options);
            var directory = Path.GetDirectoryName(_path);
            if (!string.IsNullOrEmpty(directory))
            {
                Directory.CreateDirectory(directory);
            }
            File.WriteAllText(_path, json);
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Failed to save config to {_path}: {ex.Message}");
        }
    }
}
