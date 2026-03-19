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
                m.Add("Command", context.Command.Name);
                m.Add("Status", "Success");
                m.Add("DurationMs", sw.ElapsedMilliseconds);
            });
        }
        catch (Exception ex)
        {
            sw.Stop();
            _diagnosticBus.Publish("CommandDispatcher", $"Command {context.Command.Name} failed", DiagnosticSeverity.Error, m =>
            {
                m.Add("Command", context.Command.Name);
                m.Add("Status", "Failure");
                m.Add("Error", ex.Message);
                m.Add("DurationMs", sw.ElapsedMilliseconds);
            });
            throw;
        }
    }
}
