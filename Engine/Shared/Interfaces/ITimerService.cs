using System;
using System.Threading.Tasks;

namespace Shared.Interfaces;
    /// <summary>
    /// Manages timed events and callbacks for efficient resource management.
    /// </summary>
    public interface ITimerService
    {
        /// <summary>
        /// Registers a callback to be executed at a specific time.
        /// </summary>
        void AddTimer(DateTime executeAt, Action callback);

        /// <summary>
        /// Registers a callback to be executed after a delay.
        /// </summary>
        void AddTimer(TimeSpan delay, Action callback);

        /// <summary>
        /// Processes and executes all timers that have reached their execution time.
        /// </summary>
        void Tick();
    }
