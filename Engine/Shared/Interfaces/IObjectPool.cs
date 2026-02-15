namespace Shared.Interfaces
{
    /// <summary>
    /// Standard interface for object pooling to reduce GC pressure.
    /// </summary>
    /// <typeparam name="T">The type of object to pool.</typeparam>
    public interface IObjectPool<T> where T : class
    {
        /// <summary>
        /// Rents an object from the pool.
        /// </summary>
        T Rent();

        /// <summary>
        /// Returns an object to the pool.
        /// </summary>
        void Return(T obj);

        /// <summary>
        /// Reclaims excess pooled objects to reduce memory usage.
        /// </summary>
        void Shrink();
    }
}
