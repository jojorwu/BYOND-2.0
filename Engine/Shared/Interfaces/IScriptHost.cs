using System.Collections.Generic;
using System;

namespace Shared
{
    /// <summary>
    /// Manages the execution of script threads and coordinates game logic ticks.
    /// </summary>
    public interface IScriptHost
    {
        /// <summary>
        /// Executes a single logical tick for the entire system.
        /// </summary>
        void Tick();

        /// <summary>
        /// Executes a logical tick for a specific set of game objects.
        /// </summary>
        /// <param name="objectsToTick">The collection of game objects to process.</param>
        /// <param name="processGlobals">Whether to process global logic in this tick.</param>
        void Tick(IEnumerable<IGameObject> objectsToTick, bool processGlobals = false);

        /// <summary>
        /// Enqueues a command for asynchronous execution.
        /// </summary>
        /// <param name="command">The command string.</param>
        /// <param name="onResult">Callback invoked with the result of the command.</param>
        void EnqueueCommand(string command, Action<string> onResult);

        /// <summary>
        /// Registers a new script thread for execution.
        /// </summary>
        /// <param name="thread">The thread to add.</param>
        void AddThread(IScriptThread thread);

        /// <summary>
        /// Returns a list of all currently active script threads.
        /// </summary>
        List<IScriptThread> GetThreads();

        /// <summary>
        /// Updates the collection of active script threads.
        /// </summary>
        void UpdateThreads(IEnumerable<IScriptThread> threads);

        /// <summary>
        /// Executes the specified collection of script threads.
        /// </summary>
        /// <returns>A collection of threads that are still active after execution.</returns>
        IEnumerable<IScriptThread> ExecuteThreads(IEnumerable<IScriptThread> threads, IEnumerable<IGameObject> objectsToTick, bool processGlobals = false, HashSet<int>? objectIds = null);
    }
}
