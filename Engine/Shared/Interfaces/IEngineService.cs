using System.Threading;
using System.Threading.Tasks;

namespace Shared.Interfaces
{
    public interface IEngineService : IAsyncInitializable
    {
        Task StartAsync(CancellationToken cancellationToken);
        Task StopAsync(CancellationToken cancellationToken);

        /// <summary>
        /// Priority of the service. Higher priority services start first and stop last.
        /// </summary>
        int Priority => 0;
    }
}
