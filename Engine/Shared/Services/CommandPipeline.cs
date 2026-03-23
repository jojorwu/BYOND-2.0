using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Shared.Interfaces;

namespace Shared.Services;

/// <summary>
/// Orchestrates command execution through a pipeline of middleware.
/// </summary>
public class CommandPipeline : IFreezable
{
    private ICommandMiddleware[] _middlewares;
    private readonly SharedPool<MiddlewareRunner> _runnerPool = new(() => new MiddlewareRunner());

    public class MiddlewareRunner : IPoolable
    {
        private ICommandMiddleware[] _middlewares = null!;
        private CommandContext _context = null!;
        private Func<Task> _finalAction = null!;
        private int _index;
        private readonly Func<Task> _nextDelegate;

        public MiddlewareRunner()
        {
            _nextDelegate = InvokeNextAsync;
        }

        public Task ExecuteAsync(ICommandMiddleware[] middlewares, CommandContext context, Func<Task> finalAction)
        {
            _middlewares = middlewares;
            _context = context;
            _finalAction = finalAction;
            _index = 0;
            return InvokeNextAsync();
        }

        private Task InvokeNextAsync()
        {
            if (_index < _middlewares.Length)
            {
                var middleware = _middlewares[_index++];
                return middleware.ProcessAsync(_context, _nextDelegate);
            }

            return _finalAction();
        }

        public void Reset()
        {
            _middlewares = null!;
            _context = null!;
            _finalAction = null!;
        }
    }

    public CommandPipeline(IEnumerable<ICommandMiddleware> middlewares)
    {
        _middlewares = middlewares.ToArray();
    }

    public void Freeze()
    {
        // Re-ordering or further optimization of the middleware chain could happen here
        _middlewares = _middlewares.ToArray();
    }

    public async Task ExecuteAsync(CommandContext context, Func<Task> finalAction)
    {
        if (_middlewares.Length == 0)
        {
            await finalAction();
            return;
        }

        var runner = _runnerPool.Rent();
        try
        {
            await runner.ExecuteAsync(_middlewares, context, finalAction);
        }
        finally
        {
            _runnerPool.Return(runner);
        }
    }
}
