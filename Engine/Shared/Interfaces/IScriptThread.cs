using System;

namespace Shared
{
    /// <summary>
    /// Represents an independently executing sequence of script instructions.
    /// </summary>
    public interface IScriptThread
    {
        /// <summary>
        /// The game object this thread is associated with, if any.
        /// </summary>
        IGameObject? AssociatedObject { get; }

        /// <summary>
        /// The priority of this thread.
        /// </summary>
        ScriptThreadPriority Priority { get; set; }

        /// <summary>
        /// Total time spent executing this thread in the current/last tick.
        /// </summary>
        TimeSpan ExecutionTime { get; set; }

        /// <summary>
        /// Number of ticks this thread has been waiting to be executed.
        /// </summary>
        int WaitTicks { get; set; }
    }
}
