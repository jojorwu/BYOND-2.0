using System;
using System.Threading.Tasks;

namespace Shared.Interfaces;
    /// <summary>
    /// Defines a modular extension to the engine that can register services and systems.
    /// </summary>
    public interface IPlugin
    {
        string Name { get; }
        string Version { get; }
        Task InitializeAsync(IServiceProvider serviceProvider);
        Task ShutdownAsync();
    }
