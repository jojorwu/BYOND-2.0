using System;
using System.Collections.Generic;
using System.Threading.Tasks;

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
        void Schedule(IJob job);

        /// <summary>
        /// Schedules an action as a job.
        /// </summary>
        void Schedule(Action action);

        /// <summary>
        /// Waits for all scheduled jobs to complete.
        /// </summary>
        Task CompleteAllAsync();

        /// <summary>
        /// Executes a collection of items in parallel using the job system.
        /// </summary>
        Task ForEachAsync<T>(IEnumerable<T> source, Action<T> action);
    }
}
