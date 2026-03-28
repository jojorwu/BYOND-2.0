using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Threading;
using Shared.Interfaces;
using Microsoft.Extensions.Logging;

namespace Shared.Services;
    /// <summary>
    /// A dedicated background thread for executing jobs with minimal overhead.
    /// </summary>
    public class WorkerThread : IDisposable
    {
        [ThreadStatic]
        internal static WorkerThread? Current;

        private readonly PriorityQueue<IJob, int> _jobQueue = new();
        private readonly System.Threading.Lock _lock = new();
        private volatile int _approximateCount;
        private volatile int _totalWeight;
        public readonly ArenaAllocator Arena = new();
        private readonly Thread _thread;
        private readonly ManualResetEventSlim _wakeEvent = new(false);
        private readonly JobSystem? _jobSystem;
        private readonly Func<WorkerThread, IJob?>? _stealFunc;
        private readonly TimeProvider _timeProvider;
        private readonly ILogger? _logger;
        private bool _disposed;

        public int JobCount { get { using (_lock.EnterScope()) return _jobQueue.Count; } }
        public int ApproximateJobCount => _approximateCount;
        public int ApproximateTotalWeight => _totalWeight;
        public bool IsBusy { get; private set; }
        public DateTimeOffset LastActiveTime { get; private set; }

        public WorkerThread(string name, TimeProvider timeProvider, JobSystem? jobSystem = null, Func<WorkerThread, IJob?>? stealFunc = null, ILogger? logger = null)
        {
            _jobSystem = jobSystem;
            _stealFunc = stealFunc;
            _timeProvider = timeProvider;
            _logger = logger;
            LastActiveTime = _timeProvider.GetUtcNow();
            _thread = new Thread(Run)
            {
                Name = name,
                IsBackground = true,
                Priority = ThreadPriority.AboveNormal
            };
        }

        public void Start()
        {
            _thread.Start();
        }

        public void Enqueue(IJob job)
        {
            if (_disposed) throw new ObjectDisposedException(nameof(WorkerThread));
            using (_lock.EnterScope())
            {
                _jobQueue.Enqueue(job, -(int)job.Priority);
                _approximateCount++;
                _totalWeight += Math.Max(1, job.Weight);
            }
            _wakeEvent.Set();
        }

        public void Wake()
        {
            _wakeEvent.Set();
        }

        public bool TrySteal(out IJob? job)
        {
            using (_lock.EnterScope())
            {
                if (_jobQueue.TryDequeue(out job, out _))
                {
                    _approximateCount--;
                    _totalWeight -= Math.Max(1, job!.Weight);
                    return true;
                }
                return false;
            }
        }

        private void Run()
        {
            Current = this;
            while (!_disposed)
            {
                IJob? job = null;

                // Priority 0: Check global critical queue first
                if (_jobSystem != null && _jobSystem.CriticalQueue.TryPop(out job))
                {
                    ExecuteJob(job);
                    continue;
                }

                // Fast-path: check approximate count before locking
                if (_approximateCount > 0)
                {
                    using (_lock.EnterScope())
                    {
                        if (_jobQueue.TryDequeue(out job, out _))
                        {
                            _approximateCount--;
                            _totalWeight -= Math.Max(1, job!.Weight);
                        }
                    }
                }

                if (job != null)
                {
                    ExecuteJob(job);
                    continue; // Immediately check for next job
                }

                if (_stealFunc != null && (job = _stealFunc(this)) != null)
                {
                    ExecuteJob(job);
                    continue; // Immediately check for next job
                }

                if (_disposed) break;

                // Spin-wait before full wait to reduce latency
                if (SpinWait()) continue;

                // Wait with a small timeout to allow periodic stealing attempts and sizing updates
                _wakeEvent.Wait(5);
                _wakeEvent.Reset();
            }
        }

        private bool SpinWait()
        {
            // Tuned Spin-Waiting:
            // 500 iterations provides a better balance for high-frequency engine tasks,
            // reducing the need for expensive context switches (ManualResetEvent)
            // when jobs are being produced rapidly.
            var sw = new SpinWait();
            for (int i = 0; i < 500; i++)
            {
                if (_disposed) return false;
                if (_approximateCount > 0) return true;
                sw.SpinOnce();
            }

            return false;
        }

        internal void ExecuteJob(IJob job)
        {
            IsBusy = true;
            LastActiveTime = _timeProvider.GetUtcNow();
            try
            {
                job.ExecuteAsync().GetAwaiter().GetResult();
            }
            catch (Exception ex)
            {
                if (_logger != null)
                {
                    _logger.LogError(ex, "Error executing job in {ThreadName}. Job: {JobType}, Priority: {Priority}",
                        Thread.CurrentThread.Name, job.GetType().Name, job.Priority);
                }
                else
                {
                    Console.WriteLine($"Error executing job in {Thread.CurrentThread.Name}: {ex}. Job: {job.GetType().Name}, Priority: {job.Priority}");
                }
            }
            finally
            {
                IsBusy = false;
                Arena.Reset(); // Reclaim memory between jobs
            }
        }

        public void Dispose()
        {
            if (_disposed) return;
            _disposed = true;
            _wakeEvent.Set();
            _thread.Join(1000);
            Arena.Dispose();
            _wakeEvent.Dispose();
        }
    }
