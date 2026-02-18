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
        private static readonly object _globalHierarchyLock = new();

        public static void EnsureNextId(int id)
        {
            int current;
            do
            {
                current = nextId;
                if (current > id) break;
            } while (Interlocked.CompareExchange(ref nextId, id + 1, current) != current);
        }

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
        public int X
        {
            get { lock (_lock) return _x; }
            set
            {
                var ot = ObjectType;
                if (ot != null && ot.IndexX != -1) SetVariableDirect(ot.IndexX, value);
                else { lock (_lock) { if (_x != value) { _x = value; SyncVariable("x", value); } } }
            }
        }

        /// <summary>
        /// Gets the committed X-coordinate, used for consistent reads across threads.
        /// </summary>
        public int CommittedX => _committedX;

        private int _y;
        private int _committedY;
        /// <summary>
        /// Gets or sets the Y-coordinate of the game object.
        /// </summary>
        public int Y
        {
            get { lock (_lock) return _y; }
            set
            {
                var ot = ObjectType;
                if (ot != null && ot.IndexY != -1) SetVariableDirect(ot.IndexY, value);
                else { lock (_lock) { if (_y != value) { _y = value; SyncVariable("y", value); } } }
            }
        }

        /// <summary>
        /// Gets the committed Y-coordinate, used for consistent reads across threads.
        /// </summary>
        public int CommittedY => _committedY;

        private int _z;
        private int _committedZ;
        /// <summary>
        /// Gets or sets the Z-coordinate of the game object.
        /// </summary>
        public int Z
        {
            get { lock (_lock) return _z; }
            set
            {
                var ot = ObjectType;
                if (ot != null && ot.IndexZ != -1) SetVariableDirect(ot.IndexZ, value);
                else { lock (_lock) { if (_z != value) { _z = value; SyncVariable("z", value); } } }
            }
        }

        /// <summary>
        /// Gets the committed Z-coordinate, used for consistent reads across threads.
        /// </summary>
        public int CommittedZ => _committedZ;

        private string _icon = string.Empty;
        public string Icon
        {
            get => _icon;
            set
            {
                var ot = ObjectType;
                if (ot != null && ot.IndexIcon != -1) SetVariableDirect(ot.IndexIcon, value);
                else { lock (_lock) { if (_icon != value) { _icon = value; SyncVariable("icon", value); } } }
            }
        }

        private string _iconState = string.Empty;
        public string IconState
        {
            get => _iconState;
            set
            {
                var ot = ObjectType;
                if (ot != null && ot.IndexIconState != -1) SetVariableDirect(ot.IndexIconState, value);
                else { lock (_lock) { if (_iconState != value) { _iconState = value; SyncVariable("icon_state", value); } } }
            }
        }

        private int _dir = 2;
        public int Dir
        {
            get => _dir;
            set
            {
                var ot = ObjectType;
                if (ot != null && ot.IndexDir != -1) SetVariableDirect(ot.IndexDir, (float)value);
                else { lock (_lock) { if (_dir != value) { _dir = value; SyncVariable("dir", (float)value); } } }
            }
        }

        private float _alpha = 255.0f;
        public float Alpha
        {
            get => _alpha;
            set
            {
                var ot = ObjectType;
                if (ot != null && ot.IndexAlpha != -1) SetVariableDirect(ot.IndexAlpha, value);
                else { lock (_lock) { if (_alpha != value) { _alpha = value; SyncVariable("alpha", value); } } }
            }
        }

        private string _color = "#ffffff";
        public string Color
        {
            get => _color;
            set
            {
                var ot = ObjectType;
                if (ot != null && ot.IndexColor != -1) SetVariableDirect(ot.IndexColor, value);
                else { lock (_lock) { if (_color != value) { _color = value; SyncVariable("color", value); } } }
            }
        }

        private float _layer = 2.0f;
        public float Layer
        {
            get => _layer;
            set
            {
                var ot = ObjectType;
                if (ot != null && ot.IndexLayer != -1) SetVariableDirect(ot.IndexLayer, value);
                else { lock (_lock) { if (_layer != value) { _layer = value; SyncVariable("layer", value); } } }
            }
        }

        private float _pixelX = 0;
        public float PixelX
        {
            get => _pixelX;
            set
            {
                var ot = ObjectType;
                if (ot != null && ot.IndexPixelX != -1) SetVariableDirect(ot.IndexPixelX, value);
                else { lock (_lock) { if (_pixelX != value) { _pixelX = value; SyncVariable("pixel_x", value); } } }
            }
        }

        private float _pixelY = 0;
        public float PixelY
        {
            get => _pixelY;
            set
            {
                var ot = ObjectType;
                if (ot != null && ot.IndexPixelY != -1) SetVariableDirect(ot.IndexPixelY, value);
                else { lock (_lock) { if (_pixelY != value) { _pixelY = value; SyncVariable("pixel_y", value); } } }
            }
        }

        /// <summary>
        /// Commits the current state to the read-only buffer.
        /// </summary>
        public void CommitState()
        {
            lock (_lock)
            {
                _committedX = _x;
                _committedY = _y;
                _committedZ = _z;
            }
        }

        private IGameObject? _loc;
        /// <summary>
        /// Gets or sets the location of the game object.
        /// </summary>
        public IGameObject? Loc
        {
            get { lock (_lock) return _loc; }
            set => SetLocInternal(value, true);
        }

        private void SetLocInternal(IGameObject? value, bool syncVariable)
        {
            GameObject? oldLoc = null;
            GameObject? newLoc = null;

            lock (_globalHierarchyLock)
            {
                lock (_lock)
                {
                    if (_loc == value) return;

                    oldLoc = _loc as GameObject;
                    _loc = value;
                    newLoc = value as GameObject;

                    if (syncVariable)
                    {
                        // Syncing "loc" variable which might be tracked for snapshots
                        SyncVariable("loc", value != null ? new DreamValue((DreamObject)value) : DreamValue.Null);
                    }
                }

                oldLoc?.RemoveContentInternal(this);
                newLoc?.AddContentInternal(this);
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
            if (obj is GameObject gameObj)
            {
                gameObj.Loc = this;
            }
            else
            {
                lock (_globalHierarchyLock)
                {
                    AddContentInternal(obj);
                }
            }
        }

        public virtual void RemoveContent(IGameObject obj)
        {
            if (obj is GameObject gameObj)
            {
                if (gameObj.Loc == this)
                {
                    gameObj.Loc = null;
                }
            }
            else
            {
                lock (_globalHierarchyLock)
                {
                    RemoveContentInternal(obj);
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

        /// <summary>
        /// Gets the value of a variable by name.
        /// </summary>
        public override DreamValue GetVariable(string name)
        {
            // Fast-path for common single-character built-in variables
            if (name.Length == 1)
            {
                return name[0] switch
                {
                    'x' => new DreamValue((float)X),
                    'y' => new DreamValue((float)Y),
                    'z' => new DreamValue((float)Z),
                    _ => base.GetVariable(name)
                };
            }

            return name switch
            {
                "loc" => Loc is DreamObject locObj ? new DreamValue(locObj) : DreamValue.Null,
                "icon" => new DreamValue(Icon),
                "icon_state" => new DreamValue(IconState),
                "dir" => new DreamValue((float)Dir),
                "alpha" => new DreamValue(Alpha),
                "color" => new DreamValue(Color),
                "layer" => new DreamValue(Layer),
                "pixel_x" => new DreamValue(PixelX),
                "pixel_y" => new DreamValue(PixelY),
                "name" => GetNameVariable(),
                _ => base.GetVariable(name)
            };
        }

        private DreamValue GetNameVariable()
        {
            var val = base.GetVariable("name");
            return !val.IsNull ? val : new DreamValue(ObjectType?.Name ?? "object");
        }

        /// <summary>
        /// Sets the value of a variable by name.
        /// </summary>
        public override void SetVariable(string name, DreamValue value)
        {
            if (name.Length == 1)
            {
                switch (name[0])
                {
                    case 'x': X = (int)value.GetValueAsFloat(); return;
                    case 'y': Y = (int)value.GetValueAsFloat(); return;
                    case 'z': Z = (int)value.GetValueAsFloat(); return;
                }
            }

            lock (_lock)
            {
                switch (name)
                {
                    case "loc":
                        Loc = (value.TryGetValue(out DreamObject? locObj) && locObj is IGameObject loc) ? loc : null;
                        break;
                    case "icon":
                    case "icon_state":
                    case "dir":
                    case "alpha":
                    case "color":
                    case "layer":
                    case "pixel_x":
                    case "pixel_y":
                        SyncVariable(name, value);
                        break;
                    default:
                        base.SetVariable(name, value);
                        break;
                }
            }
        }

        /// <summary>
        /// Sets the value of a variable directly by its index.
        /// </summary>
        public override void SetVariableDirect(int index, DreamValue value)
        {
            lock (_lock)
            {
                base.SetVariableDirect(index, value);

                var ot = ObjectType;
                if (ot == null) return;

                if (index == ot.IndexX) { _x = (int)value.GetValueAsFloat(); return; }
                if (index == ot.IndexY) { _y = (int)value.GetValueAsFloat(); return; }
                if (index == ot.IndexZ) { _z = (int)value.GetValueAsFloat(); return; }
                if (index == ot.IndexLoc) { SetLocInternal((value.TryGetValue(out DreamObject? locObj) && locObj is IGameObject loc) ? loc : null, false); return; }
                if (index == ot.IndexIcon) { if (value.TryGetValue(out string? icon)) _icon = icon; return; }
                if (index == ot.IndexIconState) { if (value.TryGetValue(out string? iconState)) _iconState = iconState; return; }
                if (index == ot.IndexDir) { _dir = (int)value.GetValueAsFloat(); return; }
                if (index == ot.IndexAlpha) { _alpha = value.GetValueAsFloat(); return; }
                if (index == ot.IndexColor) { if (value.TryGetValue(out string? color)) _color = color; return; }
                if (index == ot.IndexLayer) { _layer = value.GetValueAsFloat(); return; }
                if (index == ot.IndexPixelX) { _pixelX = value.GetValueAsFloat(); return; }
                if (index == ot.IndexPixelY) { _pixelY = value.GetValueAsFloat(); return; }
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
            IncrementVersion();
        }

        public void AddComponent<T>(T component) where T : class, IComponent
        {
            AddComponent((IComponent)component);
        }

        /// <summary>
        /// Adds multiple components to the game object in a single operation, reducing array reallocations.
        /// </summary>
        public void AddComponentBatch(IEnumerable<IComponent> components)
        {
            if (_componentManager == null) throw new System.InvalidOperationException("ComponentManager not set.");

            var componentsList = components.ToList();
            if (componentsList.Count == 0) return;

            foreach (var component in componentsList)
            {
                _componentManager.AddComponent(this, component);
            }

            lock (_componentCacheLock)
            {
                var current = _componentCache;
                var updatedList = current.ToList();

                foreach (var component in componentsList)
                {
                    var type = component.GetType();
                    bool found = false;
                    for (int i = 0; i < updatedList.Count; i++)
                    {
                        if (updatedList[i].GetType() == type)
                        {
                            updatedList[i] = component;
                            found = true;
                            break;
                        }
                    }

                    if (!found)
                    {
                        updatedList.Add(component);
                    }
                }

                _componentCache = updatedList.ToArray();
            }
            IncrementVersion();
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
            IncrementVersion();
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
            SetLocInternal(null, false);
            lock (_lock)
            {
                _x = 0;
                _y = 0;
                _z = 0;
                _committedX = 0;
                _committedY = 0;
                _committedZ = 0;
            }
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

    /// <summary>
    /// Custom JSON converter for <see cref="GameObject"/> to handle efficient serialization.
    /// </summary>
    public class GameObjectConverter : JsonConverter<GameObject>
    {
        /// <summary>
        /// Reads and converts the JSON to type <see cref="GameObject"/>.
        /// </summary>
        public override GameObject? Read(ref Utf8JsonReader reader, System.Type typeToConvert, JsonSerializerOptions options)
        {
            throw new System.NotImplementedException("Deserialization of GameObject via JsonConverter is not supported. Use BinarySnapshotService.");
        }

        /// <summary>
        /// Writes a <see cref="GameObject"/> value as JSON.
        /// </summary>
        public override void Write(Utf8JsonWriter writer, GameObject value, JsonSerializerOptions options)
        {
            writer.WriteStartObject();
            writer.WriteNumber("Id", value.Id);
            writer.WriteString("TypeName", value.TypeName);

            writer.WriteStartObject("Properties");
            var type = value.ObjectType;
            if (type != null)
            {
                for (int i = 0; i < type.VariableNames.Count; i++)
                {
                    var name = type.VariableNames[i];
                    var val = value.GetVariable(i);
                    if (!val.IsNull)
                    {
                        writer.WritePropertyName(name);
                        val.WriteTo(writer, options);
                    }
                }
            }
            writer.WriteEndObject();

            writer.WriteEndObject();
        }
    }
}
