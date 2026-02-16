using System;
using System.Collections.Generic;
using Shared.Interfaces;

namespace Shared.Services
{
    public class TimerService : ITimerService
    {
        private readonly PriorityQueue<Action, DateTime> _timers = new();
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
            AddTimer(DateTime.Now + delay, callback);
        }

        public void Tick()
        {
            var now = DateTime.Now;
            List<Action> toExecute = new();

            lock (_lock)
            {
                while (_timers.TryPeek(out _, out var executeAt) && executeAt <= now)
                {
                    toExecute.Add(_timers.Dequeue());
                }
            }

            foreach (var action in toExecute)
            {
                try
                {
                    action();
                }
                catch (Exception ex)
                {
                    // In a real engine, we'd log this via ILogger
                    Console.WriteLine($"Error executing timer: {ex}");
                }
            }
        }
    }
}
