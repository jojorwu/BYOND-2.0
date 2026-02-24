namespace Shared.Enums;

public enum ExecutionPhase
{
    /// <summary>
    /// Input processing and early updates.
    /// </summary>
    Input = 0,

    /// <summary>
    /// Core game logic and simulation.
    /// </summary>
    Simulation = 1,

    /// <summary>
    /// Updates that depend on simulation results (e.g. physics resolution).
    /// </summary>
    LateUpdate = 2,

    /// <summary>
    /// Preparing data for rendering and UI.
    /// </summary>
    Render = 3,

    /// <summary>
    /// Cleanup and state reset for next frame.
    /// </summary>
    Cleanup = 4
}
