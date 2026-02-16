using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Shared.Interfaces;
using Shared.Models;

namespace Shared.Services
{
    public class JobSystem : IJobSystem, IDisposable
    {
        private const int MaxTrackedJobs = 10000;
        private volatile WorkerThread[] _workers;
        private readonly ConcurrentBag<TaskCompletionSource> _pendingJobTrackers = new();
        private int _nextWorker;
        private readonly int _minWorkers;
        private readonly int _maxWorkers;
        private readonly Timer _maintenanceTimer;
        private readonly object _workerLock = new();

        public JobSystem()
        {
            _minWorkers = Math.Max(1, Environment.ProcessorCount / 2);
            _maxWorkers = Math.Max(_minWorkers, Environment.ProcessorCount * 4);

            int initialCount = Math.Max(1, Environment.ProcessorCount);
            _workers = new WorkerThread[initialCount];
            for (int i = 0; i < initialCount; i++)
            {
                _workers[i] = new WorkerThread($"Engine-Worker-{i}", TryStealJob);
            }

            foreach (var worker in _workers)
            {
                worker.Start();
            }

            _maintenanceTimer = new Timer(_ => UpdateDynamicSizing(), null, 1000, 1000);
        }

        private IJob? TryStealJob(WorkerThread stealer)
        {
            var currentWorkers = _workers;
            // Simple stealing: find the worker with the most jobs and take one
            WorkerThread? victim = null;
            int maxJobs = 0;

            for (int i = 0; i < currentWorkers.Length; i++)
            {
                var worker = currentWorkers[i];
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

        public JobHandle Schedule(IJob job, JobHandle dependency = default, bool track = true)
        {
            if (dependency.IsValid && !dependency.IsCompleted)
            {
                var tcs = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
                dependency.Task!.ContinueWith(_ =>
                {
                    var handle = ScheduleInternal(job, track);
                    handle.Task!.ContinueWith(t =>
                    {
                        if (t.IsFaulted) tcs.TrySetException(t.Exception!);
                        else if (t.IsCanceled) tcs.TrySetCanceled();
                        else tcs.TrySetResult();
                    });
                });
                return new JobHandle(tcs.Task);
            }

            return ScheduleInternal(job, track);
        }

        private JobHandle ScheduleInternal(IJob job, bool track)
        {
            var tcs = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
            if (track)
            {
                if (_pendingJobTrackers.Count > MaxTrackedJobs)
                {
                    // Emergency purge: something is scheduling tracked jobs without awaiting them
                    while (_pendingJobTrackers.TryTake(out _)) { }
                }
                _pendingJobTrackers.Add(tcs);
            }

            var finalJob = new TrackingJob(job, tcs);

            var currentWorkers = _workers;
            // Round-robin distribution
            int index = Interlocked.Increment(ref _nextWorker) % currentWorkers.Length;
            if (index < 0) index = Math.Abs(index);
            currentWorkers[index].Enqueue(finalJob);

            return new JobHandle(tcs.Task);
        }

        private void UpdateDynamicSizing()
        {
            var currentWorkers = _workers;
            int busyCount = 0;
            int totalPending = 0;
            var now = DateTime.UtcNow;

            foreach (var worker in currentWorkers)
            {
                if (worker.IsBusy) busyCount++;
                totalPending += worker.JobCount;
            }

            // Scaling rules
            if (totalPending > currentWorkers.Length * 10 || (busyCount == currentWorkers.Length && currentWorkers.Length < _maxWorkers))
            {
                AdjustWorkerCount(currentWorkers.Length + 1);
            }
            else if (currentWorkers.Length > _minWorkers)
            {
                // Find a worker that has been idle for a while
                WorkerThread? idleWorker = null;
                foreach (var worker in currentWorkers)
                {
                    if (!worker.IsBusy && worker.JobCount == 0 && (now - worker.LastActiveTime).TotalSeconds > 10)
                    {
                        idleWorker = worker;
                        break;
                    }
                }

                if (idleWorker != null)
                {
                    AdjustWorkerCount(currentWorkers.Length - 1);
                }
            }
        }

        private void AdjustWorkerCount(int targetCount)
        {
            if (targetCount < _minWorkers || targetCount > _maxWorkers) return;

            lock (_workerLock)
            {
                int currentCount = _workers.Length;
                if (targetCount == currentCount) return;

                if (targetCount > currentCount)
                {
                    // Add workers
                    var newWorkers = new WorkerThread[targetCount];
                    Array.Copy(_workers, newWorkers, currentCount);
                    for (int i = currentCount; i < targetCount; i++)
                    {
                        newWorkers[i] = new WorkerThread($"Engine-Worker-{i}", TryStealJob);
                        newWorkers[i].Start();
                    }
                    _workers = newWorkers;
                }
                else
                {
                    // Remove workers (one at a time for stability)
                    var newWorkers = new WorkerThread[targetCount];
                    Array.Copy(_workers, newWorkers, targetCount);

                    var toRemove = _workers[currentCount - 1];
                    _workers = newWorkers;

                    // Drain and dispose
                    Task.Run(() => toRemove.Dispose());
                }
            }
        }

        public JobHandle Schedule(Action action, JobHandle dependency = default, bool track = true)
        {
            return Schedule(new ActionJob(action), dependency, track);
        }

        public JobHandle CombineDependencies(params JobHandle[] dependencies)
        {
            if (dependencies == null || dependencies.Length == 0) return default;
            if (dependencies.Length == 1) return dependencies[0];

            var tasks = new List<Task>();
            for (int i = 0; i < dependencies.Length; i++)
            {
                if (dependencies[i].IsValid)
                {
                    tasks.Add(dependencies[i].Task!);
                }
            }

            if (tasks.Count == 0) return default;
            return new JobHandle(Task.WhenAll(tasks));
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

        public IArenaAllocator? GetCurrentArena()
        {
            return WorkerThread.Current?.Arena;
        }

        public async Task ResetAllArenasAsync()
        {
            await ForEachAsync(_workers, worker => worker.Arena.Reset());
        }

        public void Dispose()
        {
            _maintenanceTimer.Dispose();
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
