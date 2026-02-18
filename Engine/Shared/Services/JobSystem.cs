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
            int count = currentWorkers.Length;
            if (count <= 1) return null;

            // Power of Two Choices for stealing: pick two random victims and steal from the one with more jobs.
            // This is more efficient than a linear search across all workers as the number of workers grows.
            int i1 = Random.Shared.Next(count);
            int i2 = Random.Shared.Next(count);

            var v1 = currentWorkers[i1];
            var v2 = currentWorkers[i2];

            // Don't steal from self
            if (v1 == stealer) v1 = currentWorkers[(i1 + 1) % count];
            if (v2 == stealer) v2 = currentWorkers[(i2 + 1) % count];

            var victim = v1.ApproximateTotalWeight >= v2.ApproximateTotalWeight ? v1 : v2;

            if (victim != stealer && victim.ApproximateJobCount > 0 && victim.TrySteal(out var stolenJob))
            {
                return stolenJob;
            }

            return null;
        }

        public JobHandle Schedule(IJob job, JobHandle dependency = default, bool track = true, JobPriority priority = JobPriority.Normal)
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

        public JobHandle Schedule(IJob job, JobHandle dependency = default, bool track = true, JobPriority priority = JobPriority.Normal, int weight = 1)
        {
            // Weight is now handled via IJob.Weight, but we can wrap it if needed or use the provided one
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

            var finalJob = new TrackingJob(job, tcs, track);

            var currentWorkers = _workers;
            int count = currentWorkers.Length;
            int index;

            int preferred = job.PreferredWorkerId;
            if (preferred >= 0 && preferred < count)
            {
                index = preferred;
            }
            else if (count > 1)
            {
                // Power of Two Choices for better load balancing using Total Weight
                int i1 = Random.Shared.Next(count);
                int i2 = Random.Shared.Next(count);
                if (i1 == i2) i2 = (i1 + 1) % count;

                index = currentWorkers[i1].ApproximateTotalWeight <= currentWorkers[i2].ApproximateTotalWeight ? i1 : i2;
            }
            else
            {
                index = 0;
            }

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

        public JobHandle Schedule(Action action, JobHandle dependency = default, bool track = true, JobPriority priority = JobPriority.Normal, int weight = 1)
        {
            return Schedule(new ActionJob(action, priority, weight), dependency, track, priority);
        }

        public JobHandle Schedule(Func<Task> action, JobHandle dependency = default, bool track = true, JobPriority priority = JobPriority.Normal, int weight = 1)
        {
            return Schedule(new AsyncActionJob(action, priority, weight), dependency, track, priority);
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

        public async Task ForEachAsync<T>(IEnumerable<T> source, Action<T> action, JobPriority priority = JobPriority.Normal)
        {
            const int BatchSize = 32;
            var list = source as IReadOnlyList<T> ?? source.ToList();
            int count = list.Count;
            if (count == 0) return;

            var handles = new List<JobHandle>();

            if (count <= BatchSize)
            {
                foreach (var item in list)
                    handles.Add(Schedule(() => action(item), priority: priority, track: false));
            }
            else
            {
                for (int i = 0; i < count; i += BatchSize)
                {
                    int start = i;
                    int end = Math.Min(i + BatchSize, count);
                    handles.Add(Schedule(() =>
                    {
                        for (int j = start; j < end; j++)
                        {
                            action(list[j]);
                        }
                    }, priority: priority, track: false));
                }
            }

            await CombineDependencies(handles.ToArray()).Task!;
        }

        public async Task ForEachAsync<T>(IEnumerable<T> source, Func<T, Task> action, JobPriority priority = JobPriority.Normal)
        {
            const int BatchSize = 32;
            var list = source as IReadOnlyList<T> ?? source.ToList();
            int count = list.Count;
            if (count == 0) return;

            var handles = new List<JobHandle>();

            if (count <= BatchSize)
            {
                foreach (var item in list)
                    handles.Add(Schedule(async () => await action(item), priority: priority, track: false));
            }
            else
            {
                for (int i = 0; i < count; i += BatchSize)
                {
                    int start = i;
                    int end = Math.Min(i + BatchSize, count);
                    handles.Add(Schedule(async () =>
                    {
                        for (int j = start; j < end; j++)
                        {
                            await action(list[j]);
                        }
                    }, priority: priority, track: false));
                }
            }

            await CombineDependencies(handles.ToArray()).Task!;
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
            private readonly bool _isTracked;

            public JobPriority Priority => _inner.Priority;
            public int Weight => _inner.Weight;
            public int PreferredWorkerId => _inner.PreferredWorkerId;

            public TrackingJob(IJob inner, TaskCompletionSource tcs, bool isTracked)
            {
                _inner = inner;
                _tcs = tcs;
                _isTracked = isTracked;
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
                    if (!_isTracked)
                    {
                        Console.Error.WriteLine($"[JobSystem] Untracked job failed: {ex}");
                    }
                }
            }
        }

        private class ActionJob : IJob
        {
            private readonly Action _action;
            public JobPriority Priority { get; }
            public int Weight { get; }

            public ActionJob(Action action, JobPriority priority = JobPriority.Normal, int weight = 1)
            {
                _action = action;
                Priority = priority;
                Weight = weight;
            }

            public Task ExecuteAsync()
            {
                _action();
                return Task.CompletedTask;
            }
        }

        private class AsyncActionJob : IJob
        {
            private readonly Func<Task> _action;
            public JobPriority Priority { get; }
            public int Weight { get; }

            public AsyncActionJob(Func<Task> action, JobPriority priority = JobPriority.Normal, int weight = 1)
            {
                _action = action;
                Priority = priority;
                Weight = weight;
            }

            public Task ExecuteAsync() => _action();
        }
    }
}
