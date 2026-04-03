using System.Collections.Generic;

namespace Shared;
    /// <summary>
    /// Manages the registration and lookup of object types within the engine.
    /// </summary>
    public interface IObjectTypeManager
    {
        /// <summary>
        /// Registers a new object type.
        /// </summary>
        void RegisterObjectType(ObjectType objectType);

        /// <summary>
        /// Retrieves an object type by its name.
        /// </summary>
        ObjectType? GetObjectType(string name);

        /// <summary>
        /// Retrieves an object type by its unique numeric ID.
        /// </summary>
        ObjectType? GetObjectType(int id);

        /// <summary>
        /// Returns the number of registered object types.
        /// </summary>
        int TypeCount { get; }

        /// <summary>
        /// Returns all registered object types.
        /// </summary>
        IEnumerable<ObjectType> GetAllObjectTypes();

        /// <summary>
        /// Returns the base type definition for turfs.
        /// </summary>
        ObjectType GetTurfType();

        /// <summary>
        /// Freezes the current registry to maximize lookup performance in .NET 10.
        /// </summary>
        void Freeze();

        /// <summary>
        /// Clears all registered object types.
        /// </summary>
        void Clear();
    }
