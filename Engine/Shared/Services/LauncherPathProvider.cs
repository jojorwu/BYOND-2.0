using System;
using System.IO;
using System.Runtime.InteropServices;
using Microsoft.Extensions.Logging;
using Shared.Interfaces;
using Shared.Services;
using Shared.Attributes;

namespace Shared.Services;

public interface ILauncherPathProvider
{
    string GetExecutablePath(EngineComponent component, string basePath);
    bool IsComponentInstalled(EngineComponent component, string basePath);
}

[EngineService(typeof(ILauncherPathProvider))]
public class LauncherPathProvider : EngineService, ILauncherPathProvider
{
    private readonly ILogger<LauncherPathProvider> _logger;

    public LauncherPathProvider(ILogger<LauncherPathProvider> logger)
    {
        _logger = logger;
    }

    public string GetExecutablePath(EngineComponent component, string basePath)
    {
        string name = component switch
        {
            EngineComponent.Client => "Client",
            EngineComponent.Server => "Server",
            EngineComponent.Compiler => "Compiler",
            EngineComponent.Editor => "Editor",
            _ => throw new ArgumentOutOfRangeException(nameof(component))
        };

        if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
        {
            name += ".exe";
        }

        string path = Path.Combine(basePath, name);
        if (File.Exists(path)) return path;

        string dllName = name.Replace(".exe", "") + ".dll";
        string dllPath = Path.Combine(basePath, dllName);
        if (File.Exists(dllPath)) return dllPath;

        path = Path.Combine(basePath, "bin", name);
        if (File.Exists(path)) return path;

        dllPath = Path.Combine(basePath, "bin", dllName);
        if (File.Exists(dllPath)) return dllPath;

        return name;
    }

    public bool IsComponentInstalled(EngineComponent component, string basePath)
    {
        var path = GetExecutablePath(component, basePath);
        return File.Exists(path) || !path.Contains(Path.DirectorySeparatorChar);
    }
}
