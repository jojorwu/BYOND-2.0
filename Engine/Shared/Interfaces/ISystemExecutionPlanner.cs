using System.Collections.Generic;
using Shared.Enums;

namespace Shared.Interfaces;

/// <summary>
/// Handles the planning and scheduling of system execution based on dependencies and resource conflicts.
/// </summary>
public interface ISystemExecutionPlanner
{
    /// <summary>
    /// Calculates execution layers for a set of systems, ensuring dependencies are met and resource conflicts are minimized.
    /// </summary>
    List<List<ISystem>>[] PlanExecution(IEnumerable<ISystem> systems, ExecutionPhase[] phases);
}
