namespace Shared.Interfaces;
    /// <summary>
    /// Interface for resources that can be reclaimed during cleanup.
    /// </summary>
    public interface IShrinkable
    {
        /// <summary>
        /// Reclaims excess resources to reduce memory usage.
        /// </summary>
        void Shrink();
    }

    /// <summary>
    /// Standard interface for object pooling to reduce GC pressure.
    /// </summary>
    /// <typeparam name="T">The type of object to pool.</typeparam>
    public interface IObjectPool<T> : IShrinkable where T : class
    {
        /// <summary>
        /// Rents an object from the pool.
        /// </summary>
        T Rent();

        /// <summary>
        /// Returns an object to the pool.
        /// </summary>
        void Return(T obj);
    }
