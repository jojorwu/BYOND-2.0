using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Channels;
using System.Threading.Tasks;
using Shared.Interfaces;
using Microsoft.Extensions.Logging;

namespace Shared.Services;

public class CommandDispatcher : ICommandDispatcher, IDisposable
{
    private readonly Channel<ICommand> _commandChannel;
    private readonly ILogger<CommandDispatcher> _logger;
    private readonly IJobSystem _jobSystem;
    private readonly Task _processorTask;
    private readonly List<ICommandMiddleware> _middlewares = new();
    private volatile ICommandMiddleware[] _middlewareCache = Array.Empty<ICommandMiddleware>();
    private bool _disposed;

    public CommandDispatcher(ILogger<CommandDispatcher> logger, IJobSystem jobSystem)
    {
        _logger = logger;
        _jobSystem = jobSystem;
        _commandChannel = Channel.CreateBounded<ICommand>(new BoundedChannelOptions(1000000)
        {
            SingleReader = true,
            SingleWriter = false,
            FullMode = BoundedChannelFullMode.Wait
        });

        _processorTask = Task.Run(ProcessCommandsAsync);
    }

    public void AddMiddleware(ICommandMiddleware middleware)
    {
        lock (_middlewares)
        {
            _middlewares.Add(middleware);
            _middlewareCache = _middlewares.ToArray();
        }
    }

    public async ValueTask DispatchAsync(ICommand command)
    {
        if (_disposed) throw new ObjectDisposedException(nameof(CommandDispatcher));
        await _commandChannel.Writer.WriteAsync(command);
    }

    public async ValueTask<TResult> DispatchAsync<TResult>(ICommand<TResult> command)
    {
        if (_disposed) throw new ObjectDisposedException(nameof(CommandDispatcher));

        var context = new CommandContext(new CommandWrapper<TResult>(command));
        await ExecuteWithMiddleware(context, async () =>
        {
            context.Result = await command.ExecuteAsync();
        });

        if (context.Exception != null) throw context.Exception;
        return (TResult)context.Result!;
    }

    private async Task ExecuteWithMiddleware(CommandContext context, Func<Task> finalAction)
    {
        var middlewares = _middlewareCache;

        try
        {
            if (middlewares.Length == 0)
            {
                await finalAction();
            }
            else
            {
                int index = 0;
                Func<Task>? nextDelegate = null;
                nextDelegate = async () =>
                {
                    if (index < middlewares.Length)
                    {
                        var middleware = middlewares[index++];
                        await middleware.ProcessAsync(context, nextDelegate!);
                    }
                    else
                    {
                        await finalAction();
                    }
                };

                await nextDelegate();
            }
        }
        catch (Exception ex)
        {
            context.Exception = ex;
        }
    }

    private async Task ProcessCommandsAsync()
    {
        try
        {
            await foreach (var command in _commandChannel.Reader.ReadAllAsync())
            {
                _jobSystem.Schedule(async () =>
                {
                    var context = new CommandContext(command);
                    await ExecuteWithMiddleware(context, async () =>
                    {
                        await command.ExecuteAsync();
                    });

                    if (context.Exception != null)
                    {
                        _logger.LogError(context.Exception, "Error executing command: {CommandName}", command.Name);
                    }
                }, track: false, priority: JobPriority.Critical);
            }
        }
        catch (OperationCanceledException) { }
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        _commandChannel.Writer.TryComplete();
        _processorTask.Wait(TimeSpan.FromSeconds(5));
    }

    private class CommandWrapper<TResult> : ICommand
    {
        private readonly ICommand<TResult> _inner;
        public string Name => _inner.Name;
        public CommandWrapper(ICommand<TResult> inner) => _inner = inner;
        public Task ExecuteAsync() => _inner.ExecuteAsync();
    }
}
