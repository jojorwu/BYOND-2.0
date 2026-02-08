using System.Collections.Generic;
using System.Threading;
using System.Text.Json.Serialization;

namespace Shared
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

        private int _x;
        /// <summary>
        /// Gets or sets the X-coordinate of the game object.
        /// </summary>
        public int X { get => _x; set { _x = value; SyncVariable("x", value); } }

        private int _y;
        /// <summary>
        /// Gets or sets the Y-coordinate of the game object.
        /// </summary>
        public int Y { get => _y; set { _y = value; SyncVariable("y", value); } }

        private int _z;
        /// <summary>
        /// Gets or sets the Z-coordinate of the game object.
        /// </summary>
        public int Z { get => _z; set { _z = value; SyncVariable("z", value); } }

        private IGameObject? _loc;
        /// <summary>
        /// Gets or sets the location of the game object.
        /// </summary>
        public IGameObject? Loc
        {
            get => _loc;
            set
            {
                if (_loc == value) return;

                var oldLoc = _loc as GameObject;
                _loc = value;
                var newLoc = value as GameObject;

                oldLoc?.RemoveContentInternal(this);
                newLoc?.AddContentInternal(this);

                SyncVariable("loc", value != null ? new DreamValue((DreamObject)value) : DreamValue.Null);
            }
        }

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
                    if (_contents.Count == 0) return System.Array.Empty<IGameObject>();
                    return _contents.ToArray();
                }
            }
        }

        public virtual void AddContent(IGameObject obj)
        {
            lock (_contentsLock)
            {
                if (!_contents.Contains(obj))
                {
                    _contents.Add(obj);
                    if (obj is GameObject gameObj && gameObj.Loc != this)
                    {
                        gameObj.Loc = this;
                    }
                }
            }
        }

        public virtual void RemoveContent(IGameObject obj)
        {
            lock (_contentsLock)
            {
                if (_contents.Remove(obj))
                {
                    if (obj is GameObject gameObj && gameObj.Loc == this)
                    {
                        gameObj.Loc = null;
                    }
                }
            }
        }

        internal void AddContentInternal(IGameObject obj)
        {
            lock (_contentsLock)
            {
                if (!_contents.Contains(obj))
                    _contents.Add(obj);
            }
        }

        internal void RemoveContentInternal(IGameObject obj)
        {
            lock (_contentsLock)
            {
                _contents.Remove(obj);
            }
        }

        public override DreamValue GetVariable(string name)
        {
            switch (name)
            {
                case "x": return new DreamValue((float)X);
                case "y": return new DreamValue((float)Y);
                case "z": return new DreamValue((float)Z);
                case "loc": return Loc != null ? new DreamValue((DreamObject)Loc) : DreamValue.Null;
                case "name":
                    var val = base.GetVariable(name);
                    return !val.IsNull ? val : new DreamValue(ObjectType?.Name ?? "object");
                default:
                    return base.GetVariable(name);
            }
        }

        public override void SetVariable(string name, DreamValue value)
        {
            switch (name)
            {
                case "x": X = (int)value.GetValueAsFloat(); break;
                case "y": Y = (int)value.GetValueAsFloat(); break;
                case "z": Z = (int)value.GetValueAsFloat(); break;
                case "loc":
                    if (value.TryGetValue(out DreamObject? locObj) && locObj is IGameObject loc)
                        Loc = loc;
                    else
                        Loc = null;
                    break;
                default:
                    base.SetVariable(name, value);
                    break;
            }
        }

        private void SyncVariable(string name, DreamValue value)
        {
            base.SetVariable(name, value);
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

        public override string ToString()
        {
            return ObjectType?.Name ?? "object";
        }
    }
}
