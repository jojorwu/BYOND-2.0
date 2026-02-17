using System.Collections.Generic;
using System.Threading;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Linq;
using Shared.Interfaces;

namespace Shared
{
    /// <summary>
    /// Represents an object in the game world.
    /// </summary>
    [JsonConverter(typeof(GameObjectConverter))]
    public class GameObject : DreamObject, IGameObject, IPoolable
    {
        private static int nextId = 1;
        private IComponentManager? _componentManager;
        private volatile IComponent[] _componentCache = System.Array.Empty<IComponent>();
        private readonly object _componentCacheLock = new();

        public void SetComponentManager(IComponentManager manager) => _componentManager = manager;

        /// <summary>
        /// Gets the unique identifier for the game object.
        /// </summary>
        public int Id { get; set; }

        private int _x;
        private int _committedX;
        /// <summary>
        /// Gets or sets the X-coordinate of the game object.
        /// </summary>
        public int X { get => _x; set { if (_x != value) { _x = value; SyncVariable("x", value); Version++; } } }

        /// <summary>
        /// Gets the committed X-coordinate, used for consistent reads across threads.
        /// </summary>
        public int CommittedX => _committedX;

        private int _y;
        private int _committedY;
        /// <summary>
        /// Gets or sets the Y-coordinate of the game object.
        /// </summary>
        public int Y { get => _y; set { if (_y != value) { _y = value; SyncVariable("y", value); Version++; } } }

        /// <summary>
        /// Gets the committed Y-coordinate, used for consistent reads across threads.
        /// </summary>
        public int CommittedY => _committedY;

        private int _z;
        private int _committedZ;
        /// <summary>
        /// Gets or sets the Z-coordinate of the game object.
        /// </summary>
        public int Z { get => _z; set { if (_z != value) { _z = value; SyncVariable("z", value); Version++; } } }

        /// <summary>
        /// Gets the committed Z-coordinate, used for consistent reads across threads.
        /// </summary>
        public int CommittedZ => _committedZ;

        /// <summary>
        /// Commits the current state to the read-only buffer.
        /// </summary>
        public void CommitState()
        {
            _committedX = _x;
            _committedY = _y;
            _committedZ = _z;
        }

        private IGameObject? _loc;
        /// <summary>
        /// Gets or sets the location of the game object.
        /// </summary>
        public IGameObject? Loc
        {
            get => _loc;
            set => SetLocInternal(value, true);
        }

        private void SetLocInternal(IGameObject? value, bool syncVariable)
        {
            if (_loc == value) return;

            var oldLoc = _loc as GameObject;
            _loc = value;
            var newLoc = value as GameObject;

            oldLoc?.RemoveContentInternal(this);
            newLoc?.AddContentInternal(this);

            if (syncVariable)
            {
                SyncVariable("loc", value != null ? new DreamValue((DreamObject)value) : DreamValue.Null);
            }
        }

        protected readonly object _contentsLock = new();
        protected volatile IGameObject[] _contents = System.Array.Empty<IGameObject>();

        /// <summary>
        /// Gets the contents of this game object.
        /// </summary>
        public virtual IEnumerable<IGameObject> Contents => _contents;

        public virtual void AddContent(IGameObject obj)
        {
            lock (_contentsLock)
            {
                if (!System.Array.Exists(_contents, x => x == obj))
                {
                    var newContents = new IGameObject[_contents.Length + 1];
                    System.Array.Copy(_contents, newContents, _contents.Length);
                    newContents[_contents.Length] = obj;
                    _contents = newContents;

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
                int index = System.Array.IndexOf(_contents, obj);
                if (index != -1)
                {
                    var newContents = new IGameObject[_contents.Length - 1];
                    System.Array.Copy(_contents, 0, newContents, 0, index);
                    System.Array.Copy(_contents, index + 1, newContents, index, _contents.Length - index - 1);
                    _contents = newContents;

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
                if (!System.Array.Exists(_contents, x => x == obj))
                {
                    var newContents = new IGameObject[_contents.Length + 1];
                    System.Array.Copy(_contents, newContents, _contents.Length);
                    newContents[_contents.Length] = obj;
                    _contents = newContents;
                }
            }
        }

        internal void RemoveContentInternal(IGameObject obj)
        {
            lock (_contentsLock)
            {
                int index = System.Array.IndexOf(_contents, obj);
                if (index != -1)
                {
                    var newContents = new IGameObject[_contents.Length - 1];
                    System.Array.Copy(_contents, 0, newContents, 0, index);
                    System.Array.Copy(_contents, index + 1, newContents, index, _contents.Length - index - 1);
                    _contents = newContents;
                }
            }
        }

        public override DreamValue GetVariable(string name)
        {
            // Fast-path for common variables
            if (name.Length == 1)
            {
                if (name[0] == 'x') return new DreamValue((float)X);
                if (name[0] == 'y') return new DreamValue((float)Y);
                if (name[0] == 'z') return new DreamValue((float)Z);
            }

            switch (name)
            {
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
            if (name.Length == 1)
            {
                if (name[0] == 'x') { X = (int)value.GetValueAsFloat(); return; }
                if (name[0] == 'y') { Y = (int)value.GetValueAsFloat(); return; }
                if (name[0] == 'z') { Z = (int)value.GetValueAsFloat(); return; }
            }

            switch (name)
            {
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

        public override void SetVariableDirect(int index, DreamValue value)
        {
            base.SetVariableDirect(index, value);

            if (ObjectType != null && index >= 0 && index < ObjectType.VariableNames.Count)
            {
                var name = ObjectType.VariableNames[index];
                if (name.Length == 1)
                {
                    if (name[0] == 'x') { _x = (int)value.GetValueAsFloat(); return; }
                    if (name[0] == 'y') { _y = (int)value.GetValueAsFloat(); return; }
                    if (name[0] == 'z') { _z = (int)value.GetValueAsFloat(); return; }
                }

                if (name == "loc")
                {
                    if (value.TryGetValue(out DreamObject? locObj) && locObj is IGameObject loc)
                    {
                        SetLocInternal(loc, false);
                    }
                    else
                    {
                        SetLocInternal(null, false);
                    }
                }
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

        public void Initialize(ObjectType objectType, int x, int y, int z)
        {
            base.Initialize(objectType);
            _x = x;
            _y = y;
            _z = z;
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

        public void AddComponent(IComponent component)
        {
            if (_componentManager == null) throw new System.InvalidOperationException("ComponentManager not set.");

            _componentManager.AddComponent(this, component);
            lock (_componentCacheLock)
            {
                var type = component.GetType();
                var current = _componentCache;
                bool found = false;
                for (int i = 0; i < current.Length; i++)
                {
                    if (current[i].GetType() == type)
                    {
                        var updated = (IComponent[])current.Clone();
                        updated[i] = component;
                        _componentCache = updated;
                        found = true;
                        break;
                    }
                }

                if (!found)
                {
                    var updated = new IComponent[current.Length + 1];
                    System.Array.Copy(current, updated, current.Length);
                    updated[current.Length] = component;
                    _componentCache = updated;
                }
            }
            Version++;
        }

        public void AddComponent<T>(T component) where T : class, IComponent
        {
            AddComponent((IComponent)component);
        }

        public void RemoveComponent<T>() where T : class, IComponent
        {
            RemoveComponent(typeof(T));
        }

        public void RemoveComponent(System.Type componentType)
        {
            if (_componentManager == null) return;

            _componentManager.RemoveComponent(this, componentType);

            lock (_componentCacheLock)
            {
                var current = _componentCache;
                for (int i = 0; i < current.Length; i++)
                {
                    if (current[i].GetType() == componentType)
                    {
                        var updated = new IComponent[current.Length - 1];
                        System.Array.Copy(current, 0, updated, 0, i);
                        System.Array.Copy(current, i + 1, updated, i, current.Length - i - 1);
                        _componentCache = updated;
                        break;
                    }
                }
            }
            Version++;
        }

        public T? GetComponent<T>() where T : class, IComponent
        {
            // Lock-free fast path for already cached components
            var current = _componentCache;
            for (int i = 0; i < current.Length; i++)
            {
                if (current[i] is T component) return component;
            }
            return _componentManager?.GetComponent<T>(this);
        }

        public IEnumerable<IComponent> GetComponents()
        {
            return _componentCache;
        }

        public void SendMessage(IComponentMessage message)
        {
            // Lock-free snapshot read
            var components = _componentCache;
            for (int i = 0; i < components.Length; i++)
            {
                var component = components[i];
                if (component.Enabled)
                {
                    component.OnMessage(message);
                }
            }
        }

        public virtual void Reset()
        {
            _x = 0;
            _y = 0;
            _z = 0;
            _committedX = 0;
            _committedY = 0;
            _committedZ = 0;
            _loc = null;
            Version = 0;

            if (_componentManager != null)
            {
                // We need to notify manager about component removals during reset
                var toRemove = _componentCache;
                foreach (var component in toRemove)
                {
                    _componentManager.RemoveComponent(this, component.GetType());
                }
            }

            lock (_componentCacheLock)
            {
                _componentCache = System.Array.Empty<IComponent>();
            }

            lock (_contentsLock)
            {
                _contents = System.Array.Empty<IGameObject>();
            }

            // We don't reset Id as it should be unique for the lifetime of its registration
            // but we could if we manage IDs in the pool.
        }
    }

    public class GameObjectConverter : JsonConverter<GameObject>
    {
        public override GameObject Read(ref Utf8JsonReader reader, System.Type typeToConvert, JsonSerializerOptions options)
        {
            // Minimal implementation for now, primarily used for sending to client
            return new GameObject();
        }

        public override void Write(Utf8JsonWriter writer, GameObject value, JsonSerializerOptions options)
        {
            writer.WriteStartObject();
            writer.WriteNumber("Id", value.Id);
            writer.WriteString("TypeName", value.TypeName);
            writer.WriteStartObject("Properties");
            if (value.ObjectType != null)
            {
                for (int i = 0; i < value.ObjectType.VariableNames.Count; i++)
                {
                    var name = value.ObjectType.VariableNames[i];
                    var val = value.GetVariable(i);
                    writer.WritePropertyName(name);
                    val.WriteTo(writer, options);
                }
            }
            writer.WriteEndObject();
            writer.WriteEndObject();
        }
    }
}
