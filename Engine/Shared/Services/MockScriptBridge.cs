using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Shared.Interfaces;

namespace Shared.Services;

public class MockScriptBridge : IScriptBridge
{
    public static readonly MockScriptBridge Instance = new();

    public void RegisterFunction(IScriptFunction function) { }
    public void UnregisterFunction(string name) { }
    public ValueTask<object?> CallAsync(string name, params object?[] args) => new ValueTask<object?>((object?)null);
    public IEnumerable<IScriptFunction> GetFunctions() => Array.Empty<IScriptFunction>();
    public bool HasFunction(string name) => false;
}
