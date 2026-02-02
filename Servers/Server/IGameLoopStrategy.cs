using Shared.Models;
using Shared.Interfaces;
using Shared.Enums;
using Shared.Operations;
using Shared.Services;
using System.Threading;
using System.Threading.Tasks;

namespace Server
{
    public interface IGameLoopStrategy
    {
        Task TickAsync(CancellationToken cancellationToken);
    }
}
