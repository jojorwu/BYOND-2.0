using System.Collections.Generic;
using Shared.Interfaces;

namespace Shared;
    /// <summary>
    /// Represents a primary entity within the game world.
    /// </summary>
    public interface IGameObject
    {
        /// <summary>
        /// Unique identifier for this object.
        /// </summary>
        int Id { get; }

        /// <summary>
        /// X-coordinate in the spatial grid.
        /// </summary>
        int X { get; set; }

        /// <summary>
        /// Y-coordinate in the spatial grid.
        /// </summary>
        int Y { get; set; }

        /// <summary>
        /// Z-coordinate (map level).
        /// </summary>
        int Z { get; set; }

        /// <summary>
        /// The location of this object (container or turf).
        /// </summary>
        IGameObject? Loc { get; set; }

        /// <summary>
        /// The base type definition for this object.
        /// </summary>
        ObjectType? ObjectType { get; }

        /// <summary>
        /// Collection of objects contained within this object (e.g., inventory).
        /// </summary>
        IEnumerable<IGameObject> Contents { get; }

        /// <summary>
        /// Gets the current version of the object's state.
        /// </summary>
        long Version { get; }

        /// <summary>
        /// Committed coordinates for thread-safe reading.
        /// </summary>
        int CommittedX { get; }
        int CommittedY { get; }
        int CommittedZ { get; }

        /// <summary>
        /// Commits the current state to the read-only buffer.
        /// </summary>
        void CommitState();

        /// <summary>
        /// Updates the object's 3D position in the world.
        /// </summary>
        void SetPosition(int x, int y, int z);

        /// <summary>
        /// Retrieves a variable value by its string name.
        /// </summary>
        DreamValue GetVariable(string name);

        /// <summary>
        /// Sets a variable value by its string name.
        /// </summary>
        void SetVariable(string name, DreamValue value);

        /// <summary>
        /// Retrieves a variable value by its flattened index.
        /// </summary>
        DreamValue GetVariable(int index);

        /// <summary>
        /// Sets a variable value by its flattened index.
        /// </summary>
        void SetVariable(int index, DreamValue value);

        /// <summary>
        /// Gets all components attached to this object.
        /// </summary>
        IEnumerable<IComponent> GetComponents();

        /// <summary>
        /// Gets a component of the specified type.
        /// </summary>
        T? GetComponent<T>() where T : class, IComponent;

        /// <summary>
        /// Adds a component to this object.
        /// </summary>
        void AddComponent(IComponent component);

        /// <summary>
        /// Removes a component of the specified type.
        /// </summary>
        void RemoveComponent<T>() where T : class, IComponent;

        /// <summary>
        /// Removes a component of the specified type.
        /// </summary>
        void RemoveComponent(System.Type componentType);

        /// <summary>
        /// Sends a message to all components attached to this object.
        /// </summary>
        void SendMessage(IComponentMessage message);
    }
