using System.Threading.Tasks;

namespace Shared.Interfaces
{
    public interface ICommandDispatcher
    {
        ValueTask DispatchAsync(ICommand command);
    }
}
