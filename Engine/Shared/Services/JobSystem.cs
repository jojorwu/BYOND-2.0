using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Shared.Interfaces;
using Shared.Models;
using Shared.Attributes;
using Microsoft.Extensions.Logging;

namespace Shared.Services;

[EngineService(typeof(IJobSystem))]
public class JobSystem : EngineService, IJobSystem, IDisposable, IAsyncDisposable
{
    private const int MaxTrackedJobs = 1000000;
    private volatile WorkerThread[] _workers;
    private readonly ConcurrentStack<IJob> _criticalQueue = new();
    private readonly ConcurrentBag<TaskCompletionSource> _pendingJobTrackers = new();
    private readonly int _minWorkers;
    private readonly int _maxWorkers;
    private readonly Timer _maintenanceTimer;
    private readonly System.Threading.Lock _workerLock = new();
    private readonly ILogger<JobSystem> _logger;
    private readonly IDiagnosticBus _diagnosticBus;

    private IJob? TryStealJob(WorkerThread stealer)
    {
        var currentWorkers = _workers;
        int count = currentWorkers.Length;
        if (count <= 1) return null;

        int bestIdx = -1;
        int maxWeight = -1;

        for (int i = 0; i < 4; i++)
        {
            int idx = Random.Shared.Next(count);
            var v = currentWorkers[idx];
            if (v == stealer) continue;

            int weight = v.ApproximateTotalWeight;
            if (weight > maxWeight)
            {
                maxWeight = weight;
                bestIdx = idx;
            }
        }

        if (bestIdx != -1)
        {
            var victim = currentWorkers[bestIdx];
            if (victim.ApproximateJobCount > 0 && victim.TrySteal(out var stolenJob))
            {
                return stolenJob;
            }
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
                var handle = ScheduleInternal(job, track, priority);
                handle.Task!.ContinueWith(t =>
                {
                    if (t.IsFaulted) tcs.TrySetException(t.Exception!);
                    else if (t.IsCanceled) tcs.TrySetCanceled();
                    else tcs.TrySetResult();
                });
            });
            return new JobHandle(tcs.Task);
        }

        return ScheduleInternal(job, track, priority);
    }

    public JobHandle Schedule(IJob job, JobHandle dependency, bool track, JobPriority priority, int weight)
    {
        // weight is ignored for now as it's part of the IJob interface or handled by complexity
        return Schedule(job, dependency, track, priority);
    }

    private static readonly SharedPool<TrackingJob> _trackingJobPool = new(() => new TrackingJob());

    private JobHandle ScheduleInternal(IJob job, bool track, JobPriority priority)
    {
        var tcs = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        if (track)
        {
            if (_pendingJobTrackers.Count > MaxTrackedJobs)
            {
                while (_pendingJobTrackers.TryTake(out _)) { }
            }
            _pendingJobTrackers.Add(tcs);
        }

        var finalJob = _trackingJobPool.Rent();
        finalJob.Initialize(job, tcs, track, _logger);
        tcs.Task.ContinueWith(_ => _trackingJobPool.Return(finalJob));

        if (priority == JobPriority.Critical)
        {
            _criticalQueue.Push(finalJob);
            foreach (var worker in _workers) worker.Wake();
            return new JobHandle(tcs.Task);
        }

        var currentWorkers = _workers;
        int count = currentWorkers.Length;
        int index = 0;

        int preferred = job.PreferredWorkerId;
        if (preferred >= 0 && preferred < count)
        {
            index = preferred;
        }
        else if (count > 1)
        {
            var currentWorker = WorkerThread.Current;
            if (currentWorker != null && currentWorker.ApproximateJobCount < 10)
            {
                for (int i = 0; i < count; i++)
                {
                    if (currentWorkers[i] == currentWorker)
                    {
                        index = i;
                        goto Enqueue;
                    }
                }
            }

            int i1 = Random.Shared.Next(count);
            int i2 = Random.Shared.Next(count);
            if (i1 == i2) i2 = (i1 + 1) % count;

            index = currentWorkers[i1].ApproximateTotalWeight <= currentWorkers[i2].ApproximateTotalWeight ? i1 : i2;
        }

    Enqueue:
        currentWorkers[index].Enqueue(finalJob);
        return new JobHandle(tcs.Task);
    }

    private readonly TimeProvider _timeProvider;

    public JobSystem(ILogger<JobSystem> logger, TimeProvider timeProvider, IDiagnosticBus diagnosticBus)
    {
        _logger = logger;
        _timeProvider = timeProvider;
        _diagnosticBus = diagnosticBus;
        _minWorkers = Math.Max(1, Environment.ProcessorCount / 2);
        _maxWorkers = Math.Max(_minWorkers, Environment.ProcessorCount * 16);

        int initialCount = Math.Max(1, Environment.ProcessorCount);
        _workers = new WorkerThread[initialCount];
        for (int i = 0; i < initialCount; i++)
        {
            _workers[i] = new WorkerThread($"Engine-Worker-{i}", _timeProvider, this, TryStealJob);
        }

        foreach (var worker in _workers)
        {
            worker.Start();
        }

        _maintenanceTimer = new Timer(_ => UpdateDynamicSizing(), null, 1000, 1000);
    }

    private void UpdateDynamicSizing()
    {
        var currentWorkers = _workers;
        int busyCount = 0;
        int totalPending = 0;
        var now = _timeProvider.GetUtcNow();

        foreach (var worker in currentWorkers)
        {
            if (worker.IsBusy) busyCount++;
            totalPending += worker.JobCount;
        }

        if (totalPending > currentWorkers.Length * 20 && currentWorkers.Length < _maxWorkers)
        {
            int target = Math.Min(_maxWorkers, currentWorkers.Length + (totalPending / 20));
            AdjustWorkerCount(target);
        }
        else if (totalPending > currentWorkers.Length * 10 || (busyCount == currentWorkers.Length && currentWorkers.Length < _maxWorkers))
        {
            AdjustWorkerCount(currentWorkers.Length + 1);
        }
        else if (currentWorkers.Length > _minWorkers)
        {
            int idleLongEnoughCount = 0;
            foreach (var worker in currentWorkers)
            {
                if (!worker.IsBusy && worker.JobCount == 0 && (now - worker.LastActiveTime).TotalSeconds > 30)
                {
                    idleLongEnoughCount++;
                }
            }

            if (idleLongEnoughCount > 0)
            {
                AdjustWorkerCount(Math.Max(_minWorkers, currentWorkers.Length - Math.Max(1, idleLongEnoughCount / 2)));
            }
        }

        _diagnosticBus.Publish("JobSystem", "JobSystem status update", (currentWorkers.Length, busyCount, totalPending, _criticalQueue.Count), (m, state) =>
        {
            m.Add("WorkerCount", state.Length);
            m.Add("BusyWorkers", state.busyCount);
            m.Add("PendingJobs", state.totalPending);
            m.Add("CriticalQueueSize", state.Count);
        });
    }

    private void AdjustWorkerCount(int targetCount)
    {
        if (targetCount < _minWorkers || targetCount > _maxWorkers) return;

        using (_workerLock.EnterScope())
        {
            int currentCount = _workers.Length;
            if (targetCount == currentCount) return;

            if (targetCount > currentCount)
            {
                var newWorkers = new WorkerThread[targetCount];
                Array.Copy(_workers, newWorkers, currentCount);
                for (int i = currentCount; i < targetCount; i++)
                {
                    newWorkers[i] = new WorkerThread($"Engine-Worker-{i}", _timeProvider, this, TryStealJob);
                    newWorkers[i].Start();
                }
                _workers = newWorkers;
            }
            else
            {
                int toRemoveCount = currentCount - targetCount;
                var newWorkers = new WorkerThread[targetCount];
                Array.Copy(_workers, newWorkers, targetCount);

                var toRemoveList = new List<WorkerThread>(toRemoveCount);
                for (int i = targetCount; i < currentCount; i++) toRemoveList.Add(_workers[i]);

                _workers = newWorkers;

                foreach (var worker in toRemoveList)
                {
                    Task.Run(() => worker.Dispose());
                }
            }
        }
    }

    private static readonly SharedPool<ActionJob> _actionJobPool = new(() => new ActionJob());
    private static readonly SharedPool<StateActionJobInstance> _stateActionJobPool = new(() => new StateActionJobInstance());

    private class StateActionJobInstance : IJob, IPoolable
    {
        public Action<object?>? Action;
        public object? State;
        public JobPriority Priority { get; set; }
        public int Weight { get; set; }
        public int PreferredWorkerId => -1;

        public Task ExecuteAsync()
        {
            Action?.Invoke(State);
            return Task.CompletedTask;
        }

        public void Reset()
        {
            Action = null;
            State = null;
            Priority = JobPriority.Normal;
            Weight = 1;
        }
    }

    public JobHandle Schedule(Action action, JobHandle dependency = default, bool track = true, JobPriority priority = JobPriority.Normal, int weight = 1)
    {
        var job = _actionJobPool.Rent();
        job.Initialize(action, priority, weight);
        var handle = Schedule(job, dependency, track, priority);
        handle.Task!.ContinueWith(_ => _actionJobPool.Return(job));
        return handle;
    }

    public JobHandle Schedule(Func<Task> action, JobHandle dependency = default, bool track = true, JobPriority priority = JobPriority.Normal, int weight = 1)
    {
        return Schedule(new AsyncActionJob(action, priority, weight), dependency, track, priority);
    }

    public JobHandle Schedule<TState>(Action<TState> action, TState state, JobHandle dependency = default, bool track = true, JobPriority priority = JobPriority.Normal, int weight = 1)
    {
        var job = _stateActionJobPool.Rent();
        job.Action = s => action((TState)s!);
        job.State = state;
        job.Priority = priority;
        job.Weight = weight;
        var handle = Schedule(job, dependency, track, priority);
        handle.Task!.ContinueWith(_ => _stateActionJobPool.Return(job));
        return handle;
    }

    public JobHandle CombineDependencies(params ReadOnlySpan<JobHandle> dependencies)
    {
        if (dependencies.IsEmpty) return default;
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
        while (true)
        {
            var tasks = new List<Task>();
            while (_pendingJobTrackers.TryTake(out var tcs))
            {
                tasks.Add(tcs.Task);
            }

            if (tasks.Count == 0) break;
            await Task.WhenAll(tasks);
        }
    }

    private static readonly ConcurrentDictionary<Type, object> _forEachActionJobPools = new();

    private class ForEachActionJob<T> : IJob, IPoolable
    {
        public IReadOnlyList<T>? List;
        public int Start;
        public int End;
        public Action<T>? Action;
        public JobPriority Priority { get; set; }
        public int Weight => End - Start;
        public int PreferredWorkerId => -1;

        public Task ExecuteAsync()
        {
            for (int i = Start; i < End; i++) Action!(List![i]);
            return Task.CompletedTask;
        }

        public void Reset() { List = null; Action = null; }
    }

    public async Task ForEachAsync<T>(IEnumerable<T> source, Action<T> action, JobPriority priority = JobPriority.Normal)
    {
        var list = source as IReadOnlyList<T> ?? source.ToList();
        int count = list.Count;
        if (count == 0) return;
        if (count == 1) { action(list[0]); return; }

        int workerCount = _workers.Length;
        int batchSize = Math.Max(128, (count + workerCount - 1) / workerCount);
        int taskCount = (count + batchSize - 1) / batchSize;
        var handles = System.Buffers.ArrayPool<Task>.Shared.Rent(taskCount);

        var pool = (SharedPool<ForEachActionJob<T>>)_forEachActionJobPools.GetOrAdd(typeof(T), _ => new SharedPool<ForEachActionJob<T>>(() => new ForEachActionJob<T>()));

        try
        {
            int handleIdx = 0;
            for (int i = 0; i < count; i += batchSize)
            {
                int start = i;
                int end = Math.Min(i + batchSize, count);
                var job = pool.Rent();
                job.List = list;
                job.Start = start;
                job.End = end;
                job.Action = action;
                job.Priority = priority;
                var handle = Schedule(job, track: false, priority: priority);
                _ = handle.Task!.ContinueWith(_ => pool.Return(job));
                handles[handleIdx++] = handle.Task;
            }

            for (int i = 0; i < taskCount; i++) await handles[i];
        }
        finally
        {
            System.Buffers.ArrayPool<Task>.Shared.Return(handles, clearArray: true);
        }
    }

    public async Task ForEachAsync<T>(ReadOnlyMemory<T> source, Func<T, ValueTask> action, JobPriority priority = JobPriority.Normal)
    {
        int count = source.Length;
        if (count == 0) return;
        if (count == 1) { await action(source.Span[0]); return; }

        int workerCount = _workers.Length;
        int batchSize = Math.Max(128, (count + workerCount - 1) / workerCount);
        int taskCount = (count + batchSize - 1) / batchSize;
        var handles = System.Buffers.ArrayPool<Task>.Shared.Rent(taskCount);

        try
        {
            int handleIdx = 0;
            for (int i = 0; i < count; i += batchSize)
            {
                int start = i;
                int end = Math.Min(i + batchSize, count);
                var batch = source.Slice(start, end - start);
                handles[handleIdx++] = Schedule(async () =>
                {
                    for (int j = 0; j < batch.Length; j++)
                    {
                        var vt = action(batch.Span[j]);
                        if (!vt.IsCompleted) await vt;
                        else vt.GetAwaiter().GetResult();
                    }
                }, priority: priority, track: false).Task!;
            }

            for (int i = 0; i < taskCount; i++)
            {
                await handles[i];
            }
        }
        finally
        {
            System.Buffers.ArrayPool<Task>.Shared.Return(handles, clearArray: true);
        }
    }

    public async Task ForEachAsync<T>(ReadOnlyMemory<T> source, Func<T, Task> action, JobPriority priority = JobPriority.Normal)
    {
        int count = source.Length;
        if (count == 0) return;
        if (count == 1) { await action(source.Span[0]); return; }

        int workerCount = _workers.Length;
        int batchSize = Math.Max(128, (count + workerCount - 1) / workerCount);
        int taskCount = (count + batchSize - 1) / batchSize;
        var handles = System.Buffers.ArrayPool<Task>.Shared.Rent(taskCount);

        try
        {
            int handleIdx = 0;
            for (int i = 0; i < count; i += batchSize)
            {
                int start = i;
                int end = Math.Min(i + batchSize, count);
                var batch = source.Slice(start, end - start);
                handles[handleIdx++] = Schedule(async () =>
                {
                    for (int j = 0; j < batch.Length; j++)
                    {
                        await action(batch.Span[j]);
                    }
                }, priority: priority, track: false).Task!;
            }

            for (int i = 0; i < taskCount; i++)
            {
                await handles[i];
            }
        }
        finally
        {
            System.Buffers.ArrayPool<Task>.Shared.Return(handles, clearArray: true);
        }
    }

    public async Task ForEachAsync<T>(IEnumerable<T> source, Func<T, ValueTask> action, JobPriority priority = JobPriority.Normal)
    {
        var list = source as IReadOnlyList<T> ?? source.ToList();
        int count = list.Count;
        if (count == 0) return;
        if (count == 1) { await action(list[0]); return; }

        int workerCount = _workers.Length;
        int batchSize = Math.Max(128, (count + workerCount - 1) / workerCount);
        int taskCount = (count + batchSize - 1) / batchSize;
        var handles = System.Buffers.ArrayPool<Task>.Shared.Rent(taskCount);

        try
        {
            int handleIdx = 0;
            for (int i = 0; i < count; i += batchSize)
            {
                int start = i;
                int end = Math.Min(i + batchSize, count);
                handles[handleIdx++] = Schedule(async () =>
                {
                    for (int j = start; j < end; j++)
                    {
                        var vt = action(list[j]);
                        if (!vt.IsCompleted) await vt;
                        else vt.GetAwaiter().GetResult();
                    }
                }, priority: priority, track: false).Task!;
            }

            for (int i = 0; i < taskCount; i++)
            {
                await handles[i];
            }
        }
        finally
        {
            System.Buffers.ArrayPool<Task>.Shared.Return(handles, clearArray: true);
        }
    }

    private class ForEachIndexedActionJob<T> : IJob, IPoolable
    {
        public IReadOnlyList<T>? List;
        public int Start;
        public int End;
        public Action<T, int>? Action;
        public JobPriority Priority { get; set; }
        public int Weight => End - Start;
        public int PreferredWorkerId => -1;

        public Task ExecuteAsync()
        {
            for (int i = Start; i < End; i++) Action!(List![i], i);
            return Task.CompletedTask;
        }

        public void Reset() { List = null; Action = null; }
    }

    public async Task ForEachAsync<T>(IEnumerable<T> source, Action<T, int> action, JobPriority priority = JobPriority.Normal)
    {
        var list = source as IReadOnlyList<T> ?? source.ToList();
        int count = list.Count;
        if (count == 0) return;
        if (count == 1) { action(list[0], 0); return; }

        int workerCount = _workers.Length;
        int batchSize = Math.Max(128, (count + workerCount - 1) / workerCount);
        int taskCount = (count + batchSize - 1) / batchSize;
        var handles = System.Buffers.ArrayPool<Task>.Shared.Rent(taskCount);

        var pool = (SharedPool<ForEachIndexedActionJob<T>>)_forEachActionJobPools.GetOrAdd(typeof(ForEachIndexedActionJob<T>), _ => new SharedPool<ForEachIndexedActionJob<T>>(() => new ForEachIndexedActionJob<T>()));

        try
        {
            int handleIdx = 0;
            for (int i = 0; i < count; i += batchSize)
            {
                int start = i;
                int end = Math.Min(i + batchSize, count);
                var job = pool.Rent();
                job.List = list;
                job.Start = start;
                job.End = end;
                job.Action = action;
                job.Priority = priority;
                var handle = Schedule(job, track: false, priority: priority);
                _ = handle.Task!.ContinueWith(_ => pool.Return(job));
                handles[handleIdx++] = handle.Task;
            }

            for (int i = 0; i < taskCount; i++) await handles[i];
        }
        finally
        {
            System.Buffers.ArrayPool<Task>.Shared.Return(handles, clearArray: true);
        }
    }

    public async Task ForEachAsync<T>(IEnumerable<T> source, Func<T, Task> action, JobPriority priority = JobPriority.Normal)
    {
        var list = source as IReadOnlyList<T> ?? source.ToList();
        int count = list.Count;
        if (count == 0) return;
        if (count == 1) { await action(list[0]); return; }

        int workerCount = _workers.Length;
        int batchSize = Math.Max(128, (count + workerCount - 1) / workerCount);
        int taskCount = (count + batchSize - 1) / batchSize;
        var handles = System.Buffers.ArrayPool<Task>.Shared.Rent(taskCount);

        try
        {
            int handleIdx = 0;
            for (int i = 0; i < count; i += batchSize)
            {
                int start = i;
                int end = Math.Min(i + batchSize, count);
                handles[handleIdx++] = Schedule(async () =>
                {
                    for (int j = start; j < end; j++) await action(list[j]);
                }, priority: priority, track: false).Task!;
            }

            for (int i = 0; i < taskCount; i++)
            {
                await handles[i];
            }
        }
        finally
        {
            System.Buffers.ArrayPool<Task>.Shared.Return(handles, clearArray: true);
        }
    }

    public ConcurrentStack<IJob> CriticalQueue => _criticalQueue;

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
        GC.SuppressFinalize(this);
    }

    public async ValueTask DisposeAsync()
    {
        _maintenanceTimer.Dispose();
        foreach (var worker in _workers)
        {
            _ = Task.Run(() => worker.Dispose());
        }
        await Task.Delay(100);
        GC.SuppressFinalize(this);
    }

    public override Dictionary<string, object> GetDiagnosticInfo()
    {
        var info = base.GetDiagnosticInfo();
        var workers = _workers;
        int busyCount = 0;
        int totalPending = 0;
        foreach (var worker in workers)
        {
            if (worker.IsBusy) busyCount++;
            totalPending += worker.JobCount;
        }

        info["WorkerCount"] = workers.Length;
        info["BusyWorkers"] = busyCount;
        info["PendingJobs"] = totalPending;
        info["MinWorkers"] = _minWorkers;
        info["MaxWorkers"] = _maxWorkers;
        return info;
    }

    private class TrackingJob : IJob, IPoolable
    {
        private IJob? _inner;
        private TaskCompletionSource? _tcs;
        private bool _isTracked;
        private ILogger? _logger;

        public JobPriority Priority => _inner?.Priority ?? JobPriority.Normal;
        public int Weight => _inner?.Weight ?? 1;
        public int PreferredWorkerId => _inner?.PreferredWorkerId ?? -1;

        public void Initialize(IJob inner, TaskCompletionSource tcs, bool isTracked, ILogger logger)
        {
            _inner = inner;
            _tcs = tcs;
            _isTracked = isTracked;
            _logger = logger;
        }

        public async Task ExecuteAsync()
        {
            try
            {
                await _inner!.ExecuteAsync();
                _tcs!.TrySetResult();
            }
            catch (Exception ex)
            {
                ex.Data["JobType"] = _inner!.GetType().Name;
                ex.Data["JobPriority"] = Priority.ToString();
                ex.Data["JobWeight"] = Weight;

                _tcs!.TrySetException(ex);
                if (!_isTracked)
                {
                    _logger!.LogError(ex, "Untracked job failed: {JobType} (Priority: {Priority}, Weight: {Weight})",
                        _inner.GetType().Name, Priority, Weight);
                }
            }
        }

        public void Reset()
        {
            _inner = null;
            _tcs = null;
            _isTracked = false;
            _logger = null;
        }
    }

    private class ActionJob : IJob, IPoolable
    {
        private Action? _action;
        public JobPriority Priority { get; private set; }
        public int Weight { get; private set; }
        public int PreferredWorkerId => -1;

        public void Initialize(Action action, JobPriority priority, int weight)
        {
            _action = action;
            Priority = priority;
            Weight = weight;
        }

        public Task ExecuteAsync()
        {
            _action?.Invoke();
            return Task.CompletedTask;
        }

        public void Reset()
        {
            _action = null;
            Priority = JobPriority.Normal;
            Weight = 1;
        }
    }

    private class AsyncActionJob : IJob
    {
        private readonly Func<Task> _action;
        public JobPriority Priority { get; }
        public int Weight { get; }
        public int PreferredWorkerId => -1;

        public AsyncActionJob(Func<Task> action, JobPriority priority = JobPriority.Normal, int weight = 1)
        {
            _action = action;
            Priority = priority;
            Weight = weight;
        }

        public Task ExecuteAsync() => _action();
    }

    private class StateActionJob<TState> : IJob
    {
        private readonly Action<TState> _action;
        private readonly TState _state;
        public JobPriority Priority { get; }
        public int Weight { get; }
        public int PreferredWorkerId => -1;

        public StateActionJob(Action<TState> action, TState state, JobPriority priority = JobPriority.Normal, int weight = 1)
        {
            _action = action;
            _state = state;
            Priority = priority;
            Weight = weight;
        }

        public Task ExecuteAsync()
        {
            _action(_state);
            return Task.CompletedTask;
        }
    }
}
