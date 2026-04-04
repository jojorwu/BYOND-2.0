using System.Collections.Generic;
using Shared.Interfaces;
using Shared.Models;

namespace Shared;

    public interface IComponentVisitor
    {
        void Visit(IComponent component);
    }

    /// <summary>
    /// Represents a primary entity within the game world.
    /// </summary>
    public interface IGameObject : ITransform, IVisuals
    {
        void SetUpdateListener(IEngineUpdateListener listener);

        /// <summary>
        /// Unique identifier for this object.
        /// </summary>
        long Id { get; }

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
        /// Internal ECS metadata: the archetype this object belongs to.
        /// </summary>
        object? Archetype { get; set; }

        /// <summary>
        /// Internal ECS metadata: the index of this object within its archetype.
        /// </summary>
        int ArchetypeIndex { get; set; }

        /// <summary>
        /// Internal SpatialGrid metadata: index of the object within its current grid cell array.
        /// </summary>
        int SpatialGridIndex { get; set; }

        /// <summary>
        /// Internal SpatialGrid metadata: the key of the cell this object is currently in.
        /// </summary>
        Robust.Shared.Maths.Vector3l? CurrentGridCellKey { get; set; }

        /// <summary>
        /// Commits the current state to the read-only buffer.
        /// </summary>
        void CommitState();

        /// <summary>
        /// Clears the dirty flag on this object.
        /// </summary>
        void ClearDirty();

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
        /// Internal: Gets the list of active script threads associated with this object.
        /// </summary>
        List<IScriptThread>? ActiveThreads { get; set; }

        /// <summary>
        /// Gets all components attached to this object.
        /// </summary>
        IEnumerable<IComponent> GetComponents();

        /// <summary>
        /// Visits all components attached to this object without allocation.
        /// </summary>
        void VisitComponents<T>(ref T visitor) where T : struct, IComponentVisitor, allows ref struct;

        /// <summary>
        /// Gets a component of the specified type.
        /// </summary>
        T? GetComponent<T>() where T : class, IComponent;

        /// <summary>
        /// Gets a component of the specified type from the provided chunk.
        /// Optimized for use within systems.
        /// </summary>
        T? GetComponent<T>(ArchetypeChunk<T> chunk) where T : class, IComponent;

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

        /// <summary>
        /// Gets or sets the state machine for this game object.
        /// </summary>
        IStateMachine? StateMachine { get; set; }

        /// <summary>
        /// Gets a delta state containing all changes since the last clear.
        /// </summary>
        Models.DeltaState GetDeltaState();

        /// <summary>
        /// Direct access to the variable store for high-performance iteration during serialization.
        /// </summary>
        Interfaces.IVariableStore Variables { get; }

        /// <summary>
        /// Subscribes a listener to all variable changes on this object.
        /// </summary>
        void SubscribeToVariables(IVariableChangeListener listener);

        /// <summary>
        /// Gets the mask of fields that have changed since the last clear.
        /// </summary>
        Shared.Enums.GameObjectFields GetChangeMask();

        /// <summary>
        /// Clears the field change tracking mask.
        /// </summary>
        void ClearChangeMask();


        /// <summary>
        /// Cache for the reactive state system to avoid dictionary lookups.
        /// </summary>
        object? LastDeltaBatch { get; set; }

        /// <summary>
        /// The tick version of the cached DeltaBatch.
        /// </summary>
        long LastDeltaBatchTick { get; set; }
    }
