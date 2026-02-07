using System.Collections.Generic;

namespace Shared
{
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
        /// Returns all registered object types.
        /// </summary>
        IEnumerable<ObjectType> GetAllObjectTypes();

        /// <summary>
        /// Returns the base type definition for turfs.
        /// </summary>
        ObjectType GetTurfType();

        /// <summary>
        /// Clears all registered object types.
        /// </summary>
        void Clear();
    }
}
