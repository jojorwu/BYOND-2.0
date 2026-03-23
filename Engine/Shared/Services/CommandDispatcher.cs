using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Channels;
using System.Threading.Tasks;
using Shared.Interfaces;
using Microsoft.Extensions.Logging;

namespace Shared.Services;

public class CommandDispatcher : EngineService, ICommandDispatcher, IFreezable, IDisposable
{
    private readonly Channel<ICommand> _commandChannel;
    private readonly ILogger<CommandDispatcher> _logger;
    private readonly IJobSystem _jobSystem;
    private readonly Task _processorTask;
    private readonly CommandPipeline _pipeline;
    private bool _disposed;

    public CommandDispatcher(ILogger<CommandDispatcher> logger, IJobSystem jobSystem, IEnumerable<ICommandMiddleware> middlewares)
    {
        _logger = logger;
        _jobSystem = jobSystem;
        _pipeline = new CommandPipeline(middlewares);
        _commandChannel = Channel.CreateBounded<ICommand>(new BoundedChannelOptions(1000000)
        {
            SingleReader = true,
            SingleWriter = false,
            FullMode = BoundedChannelFullMode.Wait
        });

        _processorTask = Task.Run(ProcessCommandsAsync);
    }

    public void Freeze()
    {
        _pipeline.Freeze();
    }

    public async ValueTask DispatchAsync(ICommand command)
    {
        if (_disposed) throw new ObjectDisposedException(nameof(CommandDispatcher));
        await _commandChannel.Writer.WriteAsync(command);
    }

    public async ValueTask<TResult> DispatchAsync<TResult>(ICommand<TResult> command)
    {
        if (_disposed) throw new ObjectDisposedException(nameof(CommandDispatcher));

        var wrapper = _wrapperPool.Rent();
        wrapper.Set(command);
        var context = _contextPool.Rent();
        context.Command = wrapper;

        try
        {
            await _pipeline.ExecuteAsync(context, async () =>
            {
                context.Result = await command.ExecuteAsync();
            });

            if (context.Exception != null) throw context.Exception;
            return (TResult)context.Result!;
        }
        finally
        {
            _contextPool.Return(context);
            _wrapperPool.Return(wrapper);
        }
    }

    private static readonly SharedPool<CommandContext> _contextPool = new(() => new CommandContext());
    private static readonly SharedPool<CommandWrapperPoolable> _wrapperPool = new(() => new CommandWrapperPoolable());

    private class CommandWrapperPoolable : ICommand, IPoolable
    {
        private object? _inner;
        public string Name => ((dynamic)_inner!).Name;
        public void Set(object inner) => _inner = inner;
        public Task ExecuteAsync() => ((dynamic)_inner!).ExecuteAsync();
        public void Reset() => _inner = null;
    }


    private async Task ProcessCommandsAsync()
    {
        try
        {
            await foreach (var command in _commandChannel.Reader.ReadAllAsync())
            {
                // Offload command processing to the Job System for parallel execution
                _jobSystem.Schedule(async () =>
                {
                    var context = _contextPool.Rent();
                    context.Command = command;
                    try
                    {
                        await _pipeline.ExecuteAsync(context, async () =>
                        {
                            await command.ExecuteAsync();
                        });

                        if (context.Exception != null)
                        {
                            _logger.LogError(context.Exception, "Error executing command: {CommandName}", command.Name);
                        }
                    }
                    finally
                    {
                        _contextPool.Return(context);
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
