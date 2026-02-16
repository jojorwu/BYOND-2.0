using System.Threading.Tasks;

namespace Shared.Models
{
    /// <summary>
    /// A handle to a scheduled job, allowing for dependency tracking.
    /// </summary>
    public struct JobHandle
    {
        internal readonly Task? Task;

        public JobHandle(Task task)
        {
            Task = task;
        }

        /// <summary>
        /// Gets whether the job has completed.
        /// </summary>
        public bool IsCompleted => Task?.IsCompleted ?? true;

        /// <summary>
        /// Gets whether the handle is valid.
        /// </summary>
        public bool IsValid => Task != null;

        /// <summary>
        /// Waits for the job to complete.
        /// </summary>
        public void Complete() => Task?.Wait();

        /// <summary>
        /// Asynchronously waits for the job to complete.
        /// </summary>
        public Task CompleteAsync() => Task ?? Task.CompletedTask;
    }
}
