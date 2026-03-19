using System;
using System.Collections.Generic;
using Shared.Interfaces;
using Microsoft.Extensions.Logging;

namespace Shared.Services;
    public class TimerService : EngineService, ITimerService
    {
        private readonly PriorityQueue<Action, DateTimeOffset> _timers = new();
        private readonly List<Action> _executionBuffer = new();
        private readonly object _lock = new();
        private readonly TimeProvider _timeProvider;
        private readonly ILogger<TimerService> _logger;

        public TimerService(TimeProvider timeProvider, ILogger<TimerService> logger)
        {
            _timeProvider = timeProvider;
            _logger = logger;
        }

        public void AddTimer(DateTime executeAt, Action callback)
        {
            lock (_lock)
            {
                _timers.Enqueue(callback, executeAt);
            }
        }

        public void AddTimer(TimeSpan delay, Action callback)
        {
            AddTimer(_timeProvider.GetUtcNow().UtcDateTime + delay, callback);
        }

        public void Tick()
        {
            var now = _timeProvider.GetUtcNow();

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
                        _logger.LogError(ex, "Error executing timer");
                    }
                }
                _executionBuffer.Clear();
            }
        }
    }
