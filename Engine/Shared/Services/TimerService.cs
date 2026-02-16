using System;
using System.Collections.Generic;
using Shared.Interfaces;

namespace Shared.Services
{
    public class TimerService : ITimerService
    {
        private readonly PriorityQueue<Action, DateTime> _timers = new();
        private readonly List<Action> _executionBuffer = new();
        private readonly object _lock = new();

        public void AddTimer(DateTime executeAt, Action callback)
        {
            lock (_lock)
            {
                _timers.Enqueue(callback, executeAt);
            }
        }

        public void AddTimer(TimeSpan delay, Action callback)
        {
            AddTimer(DateTime.UtcNow + delay, callback);
        }

        public void Tick()
        {
            var now = DateTime.UtcNow;

            lock (_lock)
            {
                if (_timers.Count == 0) return;

                while (_timers.TryPeek(out _, out var executeAt) && executeAt <= now)
                {
                    _executionBuffer.Add(_timers.Dequeue());
                }
            }

            if (_executionBuffer.Count > 0)
            {
                foreach (var action in _executionBuffer)
                {
                    try
                    {
                        action();
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine($"Error executing timer: {ex}");
                    }
                }
                _executionBuffer.Clear();
            }
        }
    }
}
