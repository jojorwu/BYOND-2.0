using System.Collections.Generic;

namespace Shared.Interfaces
{
    /// <summary>
    /// Central registry for managing the lifecycle and discovery of engine systems.
    /// </summary>
    public interface ISystemRegistry
    {
        /// <summary>
        /// Registers a new system with the engine.
        /// </summary>
        void Register(ISystem system);

        /// <summary>
        /// Unregisters a system from the engine.
        /// </summary>
        void Unregister(string systemName);

        /// <summary>
        /// Gets all currently registered systems.
        /// </summary>
        IEnumerable<ISystem> GetSystems();

        /// <summary>
        /// Finds a registered system by its name.
        /// </summary>
        ISystem? GetSystem(string systemName);
    }
}
