namespace Shared.Interfaces
{
    /// <summary>
    /// Represents an object that can be reset and reused in an object pool.
    /// </summary>
    public interface IPoolable
    {
        /// <summary>
        /// Resets the object state for reuse.
        /// </summary>
        void Reset();
    }
}
