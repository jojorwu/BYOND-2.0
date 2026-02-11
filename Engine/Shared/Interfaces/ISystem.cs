using System.Threading.Tasks;

namespace Shared.Interfaces
{
    /// <summary>
    /// Represents a modular logic system that is executed during each frame tick.
    /// </summary>
    public interface ISystem
    {
        /// <summary>
        /// Executes the system's logic for one tick.
        /// </summary>
        void Tick();

        /// <summary>
        /// Gets the execution priority of the system.
        /// Systems with the same priority are executed in parallel.
        /// </summary>
        int Priority => 0;

        /// <summary>
        /// Gets whether the system is enabled.
        /// </summary>
        bool Enabled => true;
    }
}
