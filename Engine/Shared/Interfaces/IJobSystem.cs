using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Shared.Models;

namespace Shared.Interfaces
{
    /// <summary>
    /// Represents a unit of work that can be executed in parallel.
    /// </summary>
    public interface IJob
    {
        Task ExecuteAsync();
    }

    /// <summary>
    /// Provides a mechanism for executing fine-grained tasks in parallel.
    /// </summary>
    public interface IJobSystem
    {
        /// <summary>
        /// Schedules a job for execution.
        /// </summary>
        /// <param name="job">The job to execute.</param>
        /// <param name="dependency">Optional dependency. This job will only run after the dependency completes.</param>
        /// <param name="track">Whether to track this job for CompleteAllAsync.</param>
        JobHandle Schedule(IJob job, JobHandle dependency = default, bool track = true);

        /// <summary>
        /// Schedules an action as a job.
        /// </summary>
        JobHandle Schedule(Action action, JobHandle dependency = default, bool track = true);

        /// <summary>
        /// Combines multiple job handles into one.
        /// </summary>
        JobHandle CombineDependencies(params JobHandle[] dependencies);

        /// <summary>
        /// Waits for all scheduled jobs to complete.
        /// </summary>
        Task CompleteAllAsync();

        /// <summary>
        /// Executes a collection of items in parallel using the job system.
        /// </summary>
        Task ForEachAsync<T>(IEnumerable<T> source, Action<T> action);

        /// <summary>
        /// Gets the arena allocator for the current worker thread.
        /// </summary>
        IArenaAllocator? GetCurrentArena();

        /// <summary>
        /// Resets all arenas across all worker threads.
        /// </summary>
        Task ResetAllArenasAsync();
    }
}
