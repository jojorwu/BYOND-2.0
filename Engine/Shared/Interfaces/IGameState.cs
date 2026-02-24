using System;
using System.Collections.Generic;

namespace Shared;
    /// <summary>
    /// Maintains the current state of the game world, including the map and active objects.
    /// Provides thread-safe access to world data.
    /// </summary>
    public interface IGameState : IDisposable
    {
        /// <summary>
        /// The current game map.
        /// </summary>
        IMap? Map { get; set; }

        /// <summary>
        /// Spatial index for rapid object location queries.
        /// </summary>
        SpatialGrid SpatialGrid { get; }

        /// <summary>
        /// All active game objects indexed by their ID.
        /// </summary>
        IDictionary<int, GameObject> GameObjects { get; }

        /// <summary>
        /// Acquires a shared read lock.
        /// </summary>
        IDisposable ReadLock();

        /// <summary>
        /// Acquires an exclusive write lock.
        /// </summary>
        IDisposable WriteLock();

        /// <summary>
        /// Returns a snapshot of all active game objects.
        /// </summary>
        IEnumerable<IGameObject> GetAllGameObjects();

        /// <summary>
        /// Executes an action for each game object under a read lock.
        /// </summary>
        void ForEachGameObject(Action<IGameObject> action);

        /// <summary>
        /// Registers a new object in the game state.
        /// </summary>
        void AddGameObject(GameObject gameObject);

        /// <summary>
        /// Removes an object from the game state.
        /// </summary>
        void RemoveGameObject(GameObject gameObject);

        /// <summary>
        /// Updates an object's spatial registration after movement.
        /// </summary>
        void UpdateGameObject(GameObject gameObject, int oldX, int oldY);
    }
