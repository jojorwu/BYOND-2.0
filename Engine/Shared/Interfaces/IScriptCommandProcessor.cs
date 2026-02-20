using System;

namespace Shared;
    /// <summary>
    /// Manages the queue of script commands and their execution results.
    /// </summary>
    public interface IScriptCommandProcessor
    {
        /// <summary>
        /// Enqueues a command for execution against the provided script manager.
        /// </summary>
        void EnqueueCommand(string command, Action<string> onResult);

        /// <summary>
        /// Processes all pending commands using the specified script manager.
        /// </summary>
        void ProcessCommands(IScriptManager scriptManager);
    }
