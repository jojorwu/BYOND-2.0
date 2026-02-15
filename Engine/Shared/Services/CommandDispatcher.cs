using System;
using System.Threading.Channels;
using System.Threading.Tasks;
using Shared.Interfaces;
using Microsoft.Extensions.Logging;

namespace Shared.Services
{
    public class CommandDispatcher : ICommandDispatcher, IDisposable
    {
        private readonly Channel<ICommand> _commandChannel;
        private readonly ILogger<CommandDispatcher> _logger;
        private readonly Task _processorTask;
        private bool _disposed;

        public CommandDispatcher(ILogger<CommandDispatcher> logger)
        {
            _logger = logger;
            _commandChannel = Channel.CreateBounded<ICommand>(new BoundedChannelOptions(1000)
            {
                SingleReader = true,
                SingleWriter = false,
                FullMode = BoundedChannelFullMode.Wait
            });

            _processorTask = Task.Run(ProcessCommandsAsync);
        }

        public async ValueTask DispatchAsync(ICommand command)
        {
            if (_disposed) throw new ObjectDisposedException(nameof(CommandDispatcher));
            await _commandChannel.Writer.WriteAsync(command);
        }

        private async Task ProcessCommandsAsync()
        {
            try
            {
                await foreach (var command in _commandChannel.Reader.ReadAllAsync())
                {
                    try
                    {
                        await command.ExecuteAsync();
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError(ex, "Error executing command: {CommandName}", command.Name);
                    }
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
    }
}
