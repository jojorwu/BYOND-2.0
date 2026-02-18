using System;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;

namespace Shared.Interfaces
{
    /// <summary>
    /// Represents a high-level engine module that can register services and systems.
    /// </summary>
    public interface IEngineModule
    {
        /// <summary>
        /// Gets the name of the module.
        /// </summary>
        string Name { get; }

        /// <summary>
        /// Whether the module is critical. Failure to initialize a critical module will abort startup.
        /// </summary>
        bool IsCritical => true;

        /// <summary>
        /// Registers services for this module in the dependency injection container.
        /// </summary>
        /// <param name="services">The service collection.</param>
        void RegisterServices(IServiceCollection services);

        /// <summary>
        /// Initializes the module after all services have been registered.
        /// </summary>
        /// <param name="serviceProvider">The service provider.</param>
        Task InitializeAsync(IServiceProvider serviceProvider);

        /// <summary>
        /// Shuts down the module.
        /// </summary>
        Task ShutdownAsync();
    }
}
