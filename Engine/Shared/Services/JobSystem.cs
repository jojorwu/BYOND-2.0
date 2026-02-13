using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Threading.Tasks;
using Shared.Interfaces;

namespace Shared.Services
{
    public class JobSystem : IJobSystem, IDisposable
    {
        private readonly WorkerThread[] _workers;
        private readonly ConcurrentBag<TaskCompletionSource> _pendingJobTrackers = new();
        private int _nextWorker;

        public JobSystem()
        {
            int workerCount = Math.Max(1, Environment.ProcessorCount);
            _workers = new WorkerThread[workerCount];
            for (int i = 0; i < workerCount; i++)
            {
                _workers[i] = new WorkerThread($"Engine-Worker-{i}", TryStealJob);
            }

            foreach (var worker in _workers)
            {
                worker.Start();
            }
        }

        private IJob? TryStealJob(WorkerThread stealer)
        {
            // Simple stealing: find the worker with the most jobs and take one
            WorkerThread? victim = null;
            int maxJobs = 0;

            for (int i = 0; i < _workers.Length; i++)
            {
                var worker = _workers[i];
                if (worker == stealer) continue;

                int jobCount = worker.JobCount;
                if (jobCount > maxJobs)
                {
                    maxJobs = jobCount;
                    victim = worker;
                }
            }

            if (victim != null && victim.TrySteal(out var stolenJob))
            {
                return stolenJob;
            }

            return null;
        }

        public void Schedule(IJob job)
        {
            var tcs = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
            _pendingJobTrackers.Add(tcs);

            var wrappedJob = new TrackingJob(job, tcs);

            // Round-robin distribution
            int index = Interlocked.Increment(ref _nextWorker) % _workers.Length;
            if (index < 0) index = Math.Abs(index);
            _workers[index].Enqueue(wrappedJob);
        }

        public void Schedule(Action action)
        {
            Schedule(new ActionJob(action));
        }

        public async Task CompleteAllAsync()
        {
            var tasks = new List<Task>();
            while (_pendingJobTrackers.TryTake(out var tcs))
            {
                tasks.Add(tcs.Task);
            }

            if (tasks.Count > 0)
            {
                await Task.WhenAll(tasks);
            }
        }

        public async Task ForEachAsync<T>(IEnumerable<T> source, Action<T> action)
        {
            foreach (var item in source)
            {
                Schedule(() => action(item));
            }
            await CompleteAllAsync();
        }

        public void Dispose()
        {
            foreach (var worker in _workers)
            {
                worker.Dispose();
            }
        }

        private class TrackingJob : IJob
        {
            private readonly IJob _inner;
            private readonly TaskCompletionSource _tcs;

            public TrackingJob(IJob inner, TaskCompletionSource tcs)
            {
                _inner = inner;
                _tcs = tcs;
            }

            public async Task ExecuteAsync()
            {
                try
                {
                    await _inner.ExecuteAsync();
                    _tcs.TrySetResult();
                }
                catch (Exception ex)
                {
                    _tcs.TrySetException(ex);
                }
            }
        }

        private class ActionJob : IJob
        {
            private readonly Action _action;

            public ActionJob(Action action)
            {
                _action = action;
            }

            public Task ExecuteAsync()
            {
                _action();
                return Task.CompletedTask;
            }
        }
    }
}
