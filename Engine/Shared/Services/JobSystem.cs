using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Threading.Tasks;
using Shared.Interfaces;

namespace Shared.Services
{
    public class JobSystem : IJobSystem
    {
        private readonly ConcurrentBag<Task> _pendingTasks = new();

        public void Schedule(IJob job)
        {
            _pendingTasks.Add(Task.Run(() => job.ExecuteAsync()));
        }

        public void Schedule(Action action)
        {
            _pendingTasks.Add(Task.Run(action));
        }

        public async Task CompleteAllAsync()
        {
            var tasks = new List<Task>();
            while (_pendingTasks.TryTake(out var task))
            {
                tasks.Add(task);
            }

            if (tasks.Count > 0)
            {
                await Task.WhenAll(tasks);
            }
        }

        public async Task ForEachAsync<T>(IEnumerable<T> source, Action<T> action)
        {
            var options = new ParallelOptions
            {
                MaxDegreeOfParallelism = Environment.ProcessorCount
            };

            await Parallel.ForEachAsync(source, options, async (item, token) =>
            {
                await Task.Run(() => action(item), token);
            });
        }
    }
}
