using System.Threading;
using System.Threading.Tasks;

namespace Server
{
    public interface IGameLoopStrategy
    {
        Task TickAsync(CancellationToken cancellationToken);
    }
}
