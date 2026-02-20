namespace Shared.Interfaces;
    /// <summary>
    /// Represents a state container that can be committed to a consistent read-only view.
    /// </summary>
    public interface ISystemState
    {
        /// <summary>
        /// Commits the current "write" state to the "read" view.
        /// </summary>
        void Commit();
    }
