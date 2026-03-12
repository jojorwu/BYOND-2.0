using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace Shared.Interfaces;

public interface IPluginManager
{
    IReadOnlyList<IPlugin> LoadedPlugins { get; }
    Task LoadPluginsAsync();
    Task<IPlugin?> LoadPluginAsync(string path);
    Task ReloadPluginAsync(string name);
}
