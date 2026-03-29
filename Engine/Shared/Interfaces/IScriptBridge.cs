using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace Shared;

/// <summary>
/// Represents a function that can be called across different script systems (DM, Lua, C#).
/// </summary>
public interface IScriptFunction
{
    string Name { get; }
    ScriptLanguage Language { get; }

    /// <summary>
    /// Executes the function with the given arguments and returns a result.
    /// Implementation should handle value conversion between its internal type and the bridge type.
    /// </summary>
    ValueTask<object?> InvokeAsync(params object?[] args);
}

/// <summary>
/// The language or script system a function belongs to.
/// </summary>
public enum ScriptLanguage
{
    DM,
    Lua,
    CSharp,
    Native
}

/// <summary>
/// Orchestrates cross-language function calls and state sharing between script systems.
/// </summary>
public interface IScriptBridge
{
    /// <summary>
    /// Registers a function from a script system to the bridge.
    /// </summary>
    void RegisterFunction(IScriptFunction function);

    /// <summary>
    /// Unregisters a function from the bridge.
    /// </summary>
    void UnregisterFunction(string name);

    /// <summary>
    /// Calls a registered function by name from any script system.
    /// </summary>
    ValueTask<object?> CallAsync(string name, params object?[] args);

    /// <summary>
    /// Gets a collection of all registered functions.
    /// </summary>
    IEnumerable<IScriptFunction> GetFunctions();

    /// <summary>
    /// Checks if a function is registered.
    /// </summary>
    bool HasFunction(string name);
}
