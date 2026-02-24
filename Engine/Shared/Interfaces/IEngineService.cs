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

    public interface IEngineService : IAsyncInitializable
    {
        Task StartAsync(CancellationToken cancellationToken);
        Task StopAsync(CancellationToken cancellationToken);

        /// <summary>
        /// Priority of the service. Higher priority services start first and stop last.
        /// </summary>
        int Priority => 0;

        /// <summary>
        /// Whether the service is critical to the engine's operation.
        /// If a critical service fails to start, the engine will shut down.
        /// </summary>
        bool IsCritical => true;

        /// <summary>
        /// Current status of the service.
        /// </summary>
        ServiceStatus Status => ServiceStatus.Stopped;
    }
