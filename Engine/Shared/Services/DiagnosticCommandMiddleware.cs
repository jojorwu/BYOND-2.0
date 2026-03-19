using System;
using System.Diagnostics;
using System.Threading.Tasks;
using Shared.Interfaces;

namespace Shared.Services;

public class DiagnosticCommandMiddleware : ICommandMiddleware
{
    private readonly IDiagnosticBus _diagnosticBus;

    public DiagnosticCommandMiddleware(IDiagnosticBus diagnosticBus)
    {
        _diagnosticBus = diagnosticBus;
    }

    public async Task ProcessAsync(CommandContext context, Func<Task> next)
    {
        var sw = Stopwatch.StartNew();
        try
        {
            await next();
            sw.Stop();
            _diagnosticBus.Publish("CommandDispatcher", $"Command {context.Command.Name} executed", DiagnosticSeverity.Info, m =>
            {
                m["Command"] = context.Command.Name;
                m["Status"] = "Success";
                m["DurationMs"] = sw.ElapsedMilliseconds;
            });
        }
        catch (Exception ex)
        {
            sw.Stop();
            _diagnosticBus.Publish("CommandDispatcher", $"Command {context.Command.Name} failed", DiagnosticSeverity.Error, m =>
            {
                m["Command"] = context.Command.Name;
                m["Status"] = "Failure";
                m["Error"] = ex.Message;
                m["DurationMs"] = sw.ElapsedMilliseconds;
            });
            throw;
        }
    }
}
