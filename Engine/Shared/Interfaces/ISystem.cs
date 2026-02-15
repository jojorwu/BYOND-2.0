using System.Threading.Tasks;

using System.Collections.Generic;

namespace Shared.Interfaces
{
    /// <summary>
    /// Represents a modular logic system that is executed during each frame tick.
    /// </summary>
    public interface ISystem
    {
        /// <summary>
        /// Unique name for the system.
        /// </summary>
        string Name => GetType().Name;

        /// <summary>
        /// Executed before the main tick phase.
        /// </summary>
        void PreTick() { }

        /// <summary>
        /// Executes the system's logic for one tick.
        /// </summary>
        /// <param name="ecb">The command buffer to record structural changes.</param>
        void Tick(IEntityCommandBuffer ecb);

        /// <summary>
        /// Executed after the main tick phase.
        /// </summary>
        void PostTick() { }

        /// <summary>
        /// Creates jobs for parallel execution during the current tick.
        /// </summary>
        IEnumerable<IJob> CreateJobs() => System.Array.Empty<IJob>();

        /// <summary>
        /// Gets the execution priority of the system.
        /// Systems with the same priority are executed in parallel.
        /// </summary>
        int Priority => 0;

        /// <summary>
        /// Systems that must be executed before this system.
        /// </summary>
        IEnumerable<string> Dependencies => System.Array.Empty<string>();

        /// <summary>
        /// The group this system belongs to.
        /// </summary>
        string? Group => null;

        /// <summary>
        /// Gets whether the system is enabled.
        /// </summary>
        bool Enabled => true;

        /// <summary>
        /// Types of resources this system reads from.
        /// Used for safe parallel scheduling.
        /// </summary>
        IEnumerable<System.Type> ReadResources => System.Array.Empty<System.Type>();

        /// <summary>
        /// Types of resources this system writes to.
        /// Used for safe parallel scheduling.
        /// </summary>
        IEnumerable<System.Type> WriteResources => System.Array.Empty<System.Type>();
    }
}
