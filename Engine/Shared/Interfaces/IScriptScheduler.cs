using System.Collections.Generic;

namespace Shared
{
    /// <summary>
    /// Handles the execution of script threads with time budgeting and object filtering.
    /// </summary>
    public interface IScriptScheduler
    {
        /// <summary>
        /// Executes the specified collection of script threads.
        /// </summary>
        /// <param name="threads">Threads to execute.</param>
        /// <param name="objectsToTick">Game objects to consider for this execution slice.</param>
        /// <param name="processGlobals">Whether to process global (non-associated) threads.</param>
        /// <param name="objectIds">Optional set of object IDs for faster filtering.</param>
        /// <returns>A collection of threads that are still active (running or sleeping).</returns>
        System.Threading.Tasks.Task<IEnumerable<IScriptThread>> ExecuteThreadsAsync(IEnumerable<IScriptThread> threads, IEnumerable<IGameObject> objectsToTick, bool processGlobals = false, HashSet<int>? objectIds = null);
    }
}
