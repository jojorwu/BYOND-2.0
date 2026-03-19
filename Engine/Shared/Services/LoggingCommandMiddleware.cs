using System;
using System.Diagnostics;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Shared.Interfaces;

namespace Shared.Services;

public class LoggingCommandMiddleware : ICommandMiddleware
{
    private readonly ILogger<LoggingCommandMiddleware> _logger;

    public LoggingCommandMiddleware(ILogger<LoggingCommandMiddleware> logger)
    {
        _logger = logger;
    }

    public async Task ProcessAsync(CommandContext context, Func<Task> next)
    {
        var sw = Stopwatch.StartNew();
        _logger.LogDebug("Executing command: {CommandName}", context.Command.Name);

        try
        {
            await next();
            sw.Stop();
            _logger.LogInformation("Successfully executed command: {CommandName} ({Elapsed}ms)", context.Command.Name, sw.ElapsedMilliseconds);
        }
        catch (Exception ex)
        {
            sw.Stop();
            _logger.LogError(ex, "Failed to execute command: {CommandName} ({Elapsed}ms)", context.Command.Name, sw.ElapsedMilliseconds);
            throw;
        }
    }
}
