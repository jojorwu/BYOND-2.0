using System;
using System.Threading.Tasks;

namespace Shared;

public interface ITimeApi
{
    /// <summary>
    /// Gets the current game time in seconds since the server started.
    /// </summary>
    double Time { get; }

    /// <summary>
    /// Spawns a task to be executed after the specified delay.
    /// </summary>
    void Spawn(TimeSpan delay, Action action);

    /// <summary>
    /// Spawns a task to be executed after the specified delay in milliseconds.
    /// </summary>
    void Spawn(int milliseconds, Action action);
}
