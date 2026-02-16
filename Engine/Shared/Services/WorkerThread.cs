using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Threading;
using Shared.Interfaces;

namespace Shared.Services
{
    /// <summary>
    /// A dedicated background thread for executing jobs with minimal overhead.
    /// </summary>
    public class WorkerThread : IDisposable
    {
        [ThreadStatic]
        internal static WorkerThread? Current;

        private readonly PriorityQueue<IJob, int> _jobQueue = new();
        private readonly object _lock = new();
        public readonly ArenaAllocator Arena = new();
        private readonly Thread _thread;
        private readonly AutoResetEvent _wakeEvent = new(false);
        private readonly Func<WorkerThread, IJob?>? _stealFunc;
        private bool _disposed;

        public int JobCount { get { lock (_lock) return _jobQueue.Count; } }
        public bool IsBusy { get; private set; }
        public DateTime LastActiveTime { get; private set; } = DateTime.UtcNow;

        public WorkerThread(string name, Func<WorkerThread, IJob?>? stealFunc = null)
        {
            _stealFunc = stealFunc;
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
            lock (_lock)
            {
                _jobQueue.Enqueue(job, -(int)job.Priority);
            }
            _wakeEvent.Set();
        }

        public bool TrySteal(out IJob? job)
        {
            lock (_lock)
            {
                return _jobQueue.TryDequeue(out job, out _);
            }
        }

        private void Run()
        {
            Current = this;
            while (true)
            {
                IJob? job = null;
                lock (_lock)
                {
                    _jobQueue.TryDequeue(out job, out _);
                }

                if (job != null)
                {
                    ExecuteJob(job);
                }
                else if (!_disposed && _stealFunc != null && (job = _stealFunc(this)) != null)
                {
                    ExecuteJob(job);
                }
                else
                {
                    if (_disposed) break;
                    _wakeEvent.WaitOne(1); // Shorter wait for better stealing responsiveness
                }
            }
        }

        internal void ExecuteJob(IJob job)
        {
            IsBusy = true;
            LastActiveTime = DateTime.UtcNow;
            try
            {
                job.ExecuteAsync().GetAwaiter().GetResult();
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error executing job in {Thread.CurrentThread.Name}: {ex}");
            }
            finally
            {
                IsBusy = false;
            }
        }

        public void Dispose()
        {
            if (_disposed) return;
            _disposed = true;
            _wakeEvent.Set();
            _thread.Join(1000);
            _wakeEvent.Dispose();
        }
    }
}
