using System.Threading;
using System.Threading.Tasks;

namespace Shared.Interfaces;
    public enum ServiceStatus
    {
        Stopped,
        Starting,
        Running,
        Stopping,
        Failed
    }

    /// <summary>
    /// Defines the lifecycle and metadata for an engine-level service.
    /// </summary>
    public interface IEngineService : IAsyncInitializable
    {
        /// <summary>
        /// Human-readable name of the service.
        /// </summary>
        string Name { get; }

        /// <summary>
        /// Starts the service asynchronously.
        /// </summary>
        Task StartAsync(CancellationToken cancellationToken);

        /// <summary>
        /// Stops the service asynchronously.
        /// </summary>
        Task StopAsync(CancellationToken cancellationToken);

        /// <summary>
        /// Priority of the service. Higher priority services start first and stop last.
        /// </summary>
        int Priority { get; }

        /// <summary>
        /// Whether the service is critical to the engine's operation.
        /// If a critical service fails to start, the engine will shut down.
        /// </summary>
        bool IsCritical { get; }

        /// <summary>
        /// Current status of the service.
        /// </summary>
        ServiceStatus Status { get; }
    }
