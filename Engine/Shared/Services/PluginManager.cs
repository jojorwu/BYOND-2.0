using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Shared.Interfaces;
using Microsoft.Extensions.Logging;

namespace Shared.Services;
    public interface IPluginManager
    {
        IReadOnlyList<IPlugin> LoadedPlugins { get; }
        Task LoadPluginsAsync();
    }

    public class PluginManager : IPluginManager
    {
        private readonly List<IPlugin> _plugins = new();
        private readonly IServiceProvider _serviceProvider;
        private readonly ISystemRegistry _systemRegistry;
        private readonly ILogger<PluginManager> _logger;

        public IReadOnlyList<IPlugin> LoadedPlugins => _plugins;

        public PluginManager(IServiceProvider serviceProvider, ISystemRegistry systemRegistry, ILogger<PluginManager> logger)
        {
            _serviceProvider = serviceProvider;
            _systemRegistry = systemRegistry;
            _logger = logger;
        }

        public async Task LoadPluginsAsync()
        {
            _logger.LogInformation("Loading engine plugins...");

            // In a real implementation, this would scan a directory for assemblies.
            // For now, it serves as an architectural placeholder for modularity.
            foreach (var plugin in _plugins)
            {
                try
                {
                    await plugin.InitializeAsync(_serviceProvider);

                    // Discover and register systems from the plugin if any
                    if (plugin is ISystem systemPlugin && systemPlugin is ISystem system)
                    {
                        _systemRegistry.Register(system);
                    }

                    _logger.LogInformation("Loaded plugin: {PluginName} v{Version}", plugin.Name, plugin.Version);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Failed to initialize plugin: {PluginName}", plugin.Name);
                }
            }
        }
    }
