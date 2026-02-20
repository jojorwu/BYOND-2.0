using System;
using System.Threading;
using System.Threading.Tasks;

namespace Shared;
    /// <summary>
    /// Manages the lifetime of script execution environments, including hot-reloading.
    /// </summary>
    public interface IScriptEnvironmentManager
    {
        /// <summary>
        /// Event fired when a new script environment has been loaded and activated.
        /// </summary>
        event Action OnEnvironmentReloaded;

        /// <summary>
        /// Starts watching for script changes and initializes the first environment.
        /// </summary>
        Task StartAsync(CancellationToken cancellationToken);

        /// <summary>
        /// Stops watching for changes.
        /// </summary>
        void Stop();

        /// <summary>
        /// Returns the current script manager for the active environment.
        /// </summary>
        IScriptManager? GetCurrentScriptManager();

        /// <summary>
        /// Returns all threads currently active in the execution environment.
        /// </summary>
        IScriptThread[] GetActiveThreads();

        /// <summary>
        /// Updates the collection of active threads in the environment.
        /// </summary>
        void UpdateActiveThreads(IEnumerable<IScriptThread> threads);

        /// <summary>
        /// Adds a new thread to the current environment.
        /// </summary>
        void AddThread(IScriptThread thread);
    }
