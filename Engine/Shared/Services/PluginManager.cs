using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.Loader;
using System.Threading.Tasks;
using Shared.Interfaces;
using Shared.Attributes;
using Microsoft.Extensions.Logging;

namespace Shared.Services;

public class PluginLoadContext : AssemblyLoadContext
{
    private readonly AssemblyDependencyResolver _resolver;

    public PluginLoadContext(string pluginPath) : base(isCollectible: true)
    {
        _resolver = new AssemblyDependencyResolver(pluginPath);
    }

    protected override Assembly? Load(AssemblyName assemblyName)
    {
        string? assemblyPath = _resolver.ResolveAssemblyToPath(assemblyName);
        if (assemblyPath != null)
        {
            return LoadFromAssemblyPath(assemblyPath);
        }

        return null;
    }

    protected override IntPtr LoadUnmanagedDll(string unmanagedDllName)
    {
        string? libraryPath = _resolver.ResolveUnmanagedDllToPath(unmanagedDllName);
        if (libraryPath != null)
        {
            return LoadUnmanagedDllFromPath(libraryPath);
        }

        return IntPtr.Zero;
    }
}

[EngineService(typeof(IPluginManager))]
public class PluginManager : EngineService, IPluginManager
{
    private readonly List<PluginEntry> _loadedPlugins = new();
    private readonly IServiceProvider _serviceProvider;
    private readonly ISystemRegistry _systemRegistry;
    private readonly ILogger<PluginManager> _logger;

    public IReadOnlyList<IPlugin> LoadedPlugins => _loadedPlugins.Select(e => e.Plugin).ToList();

    public PluginManager(IServiceProvider serviceProvider, ISystemRegistry systemRegistry, ILogger<PluginManager> logger)
    {
        _serviceProvider = serviceProvider;
        _systemRegistry = systemRegistry;
        _logger = logger;
    }

    public async Task LoadPluginsAsync()
    {
        _logger.LogInformation("Scanning for plugins...");
    }

    public async Task<IPlugin?> LoadPluginAsync(string path)
    {
        if (!File.Exists(path)) return null;

        try
        {
            var loadContext = new PluginLoadContext(path);
            var assembly = loadContext.LoadFromAssemblyPath(path);

            var pluginType = assembly.GetTypes()
                .FirstOrDefault(t => typeof(IPlugin).IsAssignableFrom(t) && !t.IsInterface && !t.IsAbstract);

            if (pluginType == null)
            {
                loadContext.Unload();
                return null;
            }

            var plugin = (IPlugin)Activator.CreateInstance(pluginType)!;
            await plugin.InitializeAsync(_serviceProvider);

            if (plugin is ISystem system)
            {
                _systemRegistry.Register(system);
            }

            _loadedPlugins.Add(new PluginEntry(plugin, loadContext, path));
            _logger.LogInformation("Loaded plugin: {PluginName} v{Version} from {Path}", plugin.Name, plugin.Version, path);
            return plugin;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to load plugin from {Path}", path);
            return null;
        }
    }

    public async Task ReloadPluginAsync(string name)
    {
        var entry = _loadedPlugins.FirstOrDefault(e => e.Plugin.Name.Equals(name, StringComparison.OrdinalIgnoreCase));
        if (entry == null) return;

        _logger.LogInformation("Reloading plugin: {PluginName}", name);

        await UnloadPluginInternalAsync(entry);
        await LoadPluginAsync(entry.Path);
    }

    private async Task UnloadPluginInternalAsync(PluginEntry entry)
    {
        try
        {
            await entry.Plugin.ShutdownAsync();
            _loadedPlugins.Remove(entry);
            entry.Context.Unload();

            for (int i = 0; i < 10 && entry.WeakReference.IsAlive; i++)
            {
                GC.Collect();
                GC.WaitForPendingFinalizers();
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error unloading plugin: {PluginName}", entry.Plugin.Name);
        }
    }

    private class PluginEntry
    {
        public IPlugin Plugin { get; }
        public PluginLoadContext Context { get; }
        public string Path { get; }
        public WeakReference WeakReference { get; }

        public PluginEntry(IPlugin plugin, PluginLoadContext context, string path)
        {
            Plugin = plugin;
            Context = context;
            Path = path;
            WeakReference = new WeakReference(context);
        }
    }
}
