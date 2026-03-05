using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;

namespace Shared.Config;

public class EnvironmentConfigProvider : IConfigProvider
{
    private readonly string _prefix;
    public string Name => "Environment";
    public bool CanSave => false;

    public EnvironmentConfigProvider(string prefix = "BYOND_")
    {
        _prefix = prefix;
    }

    public void Load(IDictionary<string, object> settings)
    {
        var envVars = Environment.GetEnvironmentVariables();
        foreach (DictionaryEntry de in envVars)
        {
            string key = (string)de.Key;
            if (key.StartsWith(_prefix))
            {
                string configKey = key.Substring(_prefix.Length).Replace("__", ".");
                settings[configKey] = de.Value!;
            }
        }
    }

    public void Save(IDictionary<string, object> settings)
    {
        throw new NotSupportedException("Saving to environment variables is not supported.");
    }
}
