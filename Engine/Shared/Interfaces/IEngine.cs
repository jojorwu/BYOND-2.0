using System.Threading.Tasks;

namespace Shared.Interfaces;

/// <summary>
/// Provides high-level control over the engine's main execution phases.
/// </summary>
public interface IEngine
{
    /// <summary>
    /// Executes a standard engine tick, including all registered ITickable services and modules.
    /// </summary>
    Task TickAsync();

    /// <summary>
    /// Performs periodic maintenance on all registered IShrinkable services.
    /// </summary>
    Task MaintainAsync();
}
