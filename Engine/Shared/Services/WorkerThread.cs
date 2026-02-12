using System;
using System.Collections.Concurrent;
using System.Threading;
using Shared.Interfaces;

namespace Shared.Services
{
    /// <summary>
    /// A dedicated background thread for executing jobs with minimal overhead.
    /// </summary>
    public class WorkerThread : IDisposable
    {
        private readonly ConcurrentQueue<IJob> _jobQueue = new();
        private readonly Thread _thread;
        private readonly AutoResetEvent _wakeEvent = new(false);
        private bool _disposed;

        public int JobCount => _jobQueue.Count;

        public WorkerThread(string name)
        {
            _thread = new Thread(Run)
            {
                Name = name,
                IsBackground = true,
                Priority = ThreadPriority.AboveNormal
            };
            _thread.Start();
        }

        public void Enqueue(IJob job)
        {
            if (_disposed) throw new ObjectDisposedException(nameof(WorkerThread));
            _jobQueue.Enqueue(job);
            _wakeEvent.Set();
        }

        private void Run()
        {
            while (!_disposed)
            {
                if (_jobQueue.TryDequeue(out var job))
                {
                    try
                    {
                        job.ExecuteAsync().GetAwaiter().GetResult();
                    }
                    catch (Exception ex)
                    {
                        // Log error - in a real implementation we would use ILogger
                        Console.WriteLine($"Error executing job in {Thread.CurrentThread.Name}: {ex}");
                    }
                }
                else
                {
                    _wakeEvent.WaitOne(10); // Wait for new work or timeout
                }
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
