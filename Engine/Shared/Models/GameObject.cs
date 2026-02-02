using Shared.Models;
using Shared.Interfaces;
using Shared.Enums;
using Shared.Operations;
using Shared.Services;
using System.Collections.Generic;
using System.Threading;
using System.Text.Json.Serialization;

namespace Shared.Models
{
    /// <summary>
    /// Represents an object in the game world.
    /// </summary>
    public class GameObject : DreamObject, IGameObject
    {
        private static int nextId = 1;

        /// <summary>
        /// Gets the unique identifier for the game object.
        /// </summary>
        public int Id { get; set; }

        /// <summary>
        /// Gets or sets the X-coordinate of the game object.
        /// </summary>
        public int X { get; set; }

        /// <summary>
        /// Gets or sets the Y-coordinate of the game object.
        /// </summary>
        public int Y { get; set; }

        /// <summary>
        /// Gets or sets the Z-coordinate of the game object.
        /// </summary>
        public int Z { get; set; }

        protected readonly object _contentsLock = new();
        protected readonly List<IGameObject> _contents = new();

        /// <summary>
        /// Gets the contents of this game object.
        /// </summary>
        public virtual IEnumerable<IGameObject> Contents
        {
            get
            {
                lock (_contentsLock)
                {
                    return new List<IGameObject>(_contents);
                }
            }
        }

        public virtual void AddContent(IGameObject obj)
        {
            lock (_contentsLock)
            {
                if (!_contents.Contains(obj))
                    _contents.Add(obj);
            }
        }

        public virtual void RemoveContent(IGameObject obj)
        {
            lock (_contentsLock)
            {
                _contents.Remove(obj);
            }
        }

        /// <summary>
        /// Gets the TypeName of this game object for serialization.
        /// </summary>
        public string TypeName => ObjectType?.Name ?? string.Empty;

        /// <summary>
        /// Gets the instance-specific properties for serialization.
        /// </summary>
        public Dictionary<string, DreamValue> Properties
        {
            get
            {
                var dict = new Dictionary<string, DreamValue>();
                if (ObjectType != null)
                {
                    for (int i = 0; i < ObjectType.VariableNames.Count; i++)
                    {
                        dict[ObjectType.VariableNames[i]] = GetVariable(i);
                    }
                }
                return dict;
            }
            set
            {
                if (value != null)
                {
                    foreach (var kvp in value)
                    {
                        SetVariable(kvp.Key, kvp.Value);
                    }
                }
            }
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="GameObject"/> class.
        /// </summary>
        /// <param name="objectType">The ObjectType of the game object.</param>
        public GameObject(ObjectType objectType) : base(objectType)
        {
            Id = Interlocked.Increment(ref nextId);
        }

        /// <summary>
        /// Parameterless constructor for deserialization.
        /// </summary>
        [JsonConstructor]
        public GameObject() : base(null!)
        {
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
            X = x;
            Y = y;
            Z = z;
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
    }
}
