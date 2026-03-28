using System.Threading.Tasks;

using System.Collections.Generic;
using Shared.Models;

namespace Shared.Interfaces;
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
        /// Called when the system is initialized.
        /// </summary>
        void Initialize() { }

        /// <summary>
        /// Called when the engine is shutting down.
        /// </summary>
        void Shutdown() { }

        /// <summary>
        /// Asynchronous version of Shutdown.
        /// </summary>
        Task ShutdownAsync() => Task.CompletedTask;

        /// <summary>
        /// Executed before the main tick phase.
        /// </summary>
        void PreTick() { }

        /// <summary>
        /// Executes the system's logic for one tick.
        /// </summary>
        /// <param name="ecb">The command buffer to record structural changes.</param>
        ValueTask TickAsync(IEntityCommandBuffer ecb)
        {
            Tick(ecb);
            return ValueTask.CompletedTask;
        }

        /// <summary>
        /// Synchronous execution for backward compatibility.
        /// </summary>
        void Tick(IEntityCommandBuffer ecb) { }

        /// <summary>
        /// Optional batch processing for archetypes that match this system's requirements.
        /// </summary>
        ValueTask TickAsync(Archetype archetype, IEntityCommandBuffer ecb)
        {
            Tick(archetype, ecb);
            return ValueTask.CompletedTask;
        }

        /// <summary>
        /// Synchronous batch processing.
        /// </summary>
        void Tick(Archetype archetype, IEntityCommandBuffer ecb) { }

        /// <summary>
        /// Executed after the main tick phase.
        /// </summary>
        void PostTick() { }

        /// <summary>
        /// Creates jobs for parallel execution during the current tick.
        /// </summary>
        IEnumerable<IJob> CreateJobs() => System.Array.Empty<IJob>();

        /// <summary>
        /// Gets the execution phase of the system.
        /// </summary>
        Enums.ExecutionPhase Phase => Enums.ExecutionPhase.Simulation;

        /// <summary>
        /// Gets the execution priority of the system within its phase.
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
        /// Gets whether matching archetypes should be processed in parallel.
        /// </summary>
        bool ParallelArchetypes => false;

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
