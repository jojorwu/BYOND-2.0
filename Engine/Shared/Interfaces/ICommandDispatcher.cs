using System.Threading.Tasks;

namespace Shared.Interfaces;
    public interface ICommandDispatcher
    {
        ValueTask DispatchAsync(ICommand command);
        ValueTask<TResult> DispatchAsync<TResult>(ICommand<TResult> command);
        void AddMiddleware(ICommandMiddleware middleware);
    }
