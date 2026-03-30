using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Shared.Interfaces;

namespace Shared.Services;

/// <summary>
/// High-performance central registry and dispatcher for cross-language script calls.
/// </summary>
public class ScriptBridge : EngineService, IScriptBridge
{
    private readonly ConcurrentDictionary<string, IScriptFunction> _functions = new(StringComparer.OrdinalIgnoreCase);
    private readonly ILogger<ScriptBridge> _logger;
    private readonly IDiagnosticBus _diagnosticBus;

    public ScriptBridge(ILogger<ScriptBridge> logger, IDiagnosticBus diagnosticBus)
    {
        _logger = logger;
        _diagnosticBus = diagnosticBus;
    }

    public void RegisterFunction(IScriptFunction function)
    {
        if (function == null) throw new ArgumentNullException(nameof(function));

        if (_functions.TryAdd(function.Name, function))
        {
            _logger.LogDebug("[ScriptBridge] Registered {Language} function: {FunctionName}", function.Language, function.Name);

            _diagnosticBus.Publish("ScriptBridge", "Function registered", DiagnosticSeverity.Info, m =>
            {
                m.Add("Name", function.Name);
                m.Add("Language", function.Language.ToString());
            });
        }
        else
        {
            _logger.LogWarning("[ScriptBridge] Failed to register function {FunctionName}. Name already exists.", function.Name);
        }
    }

    public void UnregisterFunction(string name)
    {
        if (_functions.TryRemove(name, out var function))
        {
            _logger.LogDebug("[ScriptBridge] Unregistered function: {FunctionName}", name);
        }
    }

    public async ValueTask<object?> CallAsync(string name, params object?[] args)
    {
        if (_functions.TryGetValue(name, out var function))
        {
            var sw = System.Diagnostics.Stopwatch.StartNew();
            try
            {
                var result = await function.InvokeAsync(args);
                sw.Stop();

                _diagnosticBus.Publish("ScriptBridge.Calls", "Function called", DiagnosticSeverity.Info, m =>
                {
                    m.Add("Name", name);
                    m.Add("Language", function.Language.ToString());
                    m.Add("DurationMs", sw.Elapsed.TotalMilliseconds);
                });

                return result;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "[ScriptBridge] Error calling function {FunctionName} ({Language})", name, function.Language);
                throw;
            }
        }

        _logger.LogWarning("[ScriptBridge] Call failed: Function {FunctionName} not found.", name);
        return null;
    }

    public IEnumerable<IScriptFunction> GetFunctions() => _functions.Values;

    public bool HasFunction(string name) => _functions.ContainsKey(name);

    public override Dictionary<string, object> GetDiagnosticInfo()
    {
        var info = base.GetDiagnosticInfo();
        info["RegisteredFunctions"] = _functions.Count;
        return info;
    }
}
