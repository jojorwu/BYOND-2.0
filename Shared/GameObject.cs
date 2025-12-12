using System.Collections.Generic;
using System.Threading;

namespace Shared
{
    /// <summary>
    /// Represents an object in the game world.
    /// </summary>
    public class GameObject : IGameObject
    {
        private static int nextId = 1;
        private int _x, _y, _z;

        /// <summary>
        /// Gets the unique identifier for the game object.
        /// </summary>
        public int Id { get; }

        /// <summary>
        /// Gets or sets the X-coordinate of the game object.
        /// </summary>
        public int X { get => _x; set { if(_x != value) { _x = value; IsDirty = true; } } }

        /// <summary>
        /// Gets or sets the Y-coordinate of the game object.
        /// </summary>
        public int Y { get => _y; set { if(_y != value) { _y = value; IsDirty = true; } } }

        /// <summary>
        /// Gets or sets the Z-coordinate of the game object.
        /// </summary>
        public int Z { get => _z; set { if(_z != value) { _z = value; IsDirty = true; } } }

        /// <summary>
        /// Gets the ObjectType of this game object.
        /// </summary>
        public ObjectType ObjectType { get; }

        /// <summary>
        /// Gets the instance-specific properties of this game object.
        /// </summary>
        public Dictionary<string, object?> Properties { get; } = new();

        /// <summary>
        /// Gets or sets a value indicating whether the object has changed since the last snapshot.
        /// </summary>
        public bool IsDirty { get; set; } = true;

        /// <summary>
        /// Initializes a new instance of the <see cref="GameObject"/> class.
        /// </summary>
        /// <param name="objectType">The ObjectType of the game object.</param>
        public GameObject(ObjectType objectType)
        {
            Id = Interlocked.Increment(ref nextId);
            ObjectType = objectType;
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="GameObject"/> class.
        /// </summary>
        /// <param name="objectType">The ObjectType of the game object.</param>
        /// <param name="x">The X-coordinate of the game object.</param>
        /// <param name="y">The Y-coordinate of the game object.</param>
        /// <param name="z">The Z-coordinate of the game object.</param>
        public GameObject(ObjectType objectType, int x, int y, int z) : this(objectType)
        {
            _x = x;
            _y = y;
            _z = z;
        }

        /// <summary>
        /// Sets the position of the game object.
        /// </summary>
        /// <param name="x">The new X-coordinate.</param>
        /// <param name="y">The new Y-coordinate.</param>
        /// <param name="z">The new Z-coordinate.</param>
        public void SetPosition(int x, int y, int z)
        {
            X = x;
            Y = y;
            Z = z;
        }

        /// <summary>
        /// Gets a property value, checking instance properties first, then falling back to the ObjectType's default properties.
        /// </summary>
        /// <typeparam name="T">The type of the property.</typeparam>
        /// <param name="propertyName">The name of the property.</param>
        /// <returns>The value of the property, or default(T) if not found.</returns>
        public T? GetProperty<T>(string propertyName)
        {
            if (Properties.TryGetValue(propertyName, out var value) && value is T tValue)
            {
                return tValue;
            }

            var currentObjectType = ObjectType;
            while (currentObjectType != null)
            {
                if (currentObjectType.DefaultProperties.TryGetValue(propertyName, out value) && value is T tDefaultValue)
                {
                    return tDefaultValue;
                }
                currentObjectType = currentObjectType.Parent;
            }

            return default;
        }

        /// <summary>
        /// Sets an instance-specific property value.
        /// </summary>
        /// <param name="propertyName">The name of the property.</param>
        /// <param name="value">The value to set.</param>
        public void SetProperty(string propertyName, object? value)
        {
            if (!Properties.TryGetValue(propertyName, out var oldValue) || !Equals(oldValue, value))
            {
                Properties[propertyName] = value;
                IsDirty = true;
            }
        }
    }
}
