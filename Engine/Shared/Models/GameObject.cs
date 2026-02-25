using System.Collections.Generic;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Linq;
using Shared.Interfaces;
using Shared.Enums;
using Shared.Models;

namespace Shared;

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

    public void SetComponentManager(IComponentManager manager) => _componentManager = manager;

    /// <summary>
    /// Gets the unique identifier for the game object.
    /// </summary>
    public int Id { get; set; }

    public object? Archetype { get; set; }
    public int ArchetypeIndex { get; set; }

    internal event Action<GameObject, int, int, int>? PositionChanged;

    public IGameObject? NextInGridCell { get; set; }
    public IGameObject? PrevInGridCell { get; set; }
    public long? CurrentGridCellKey { get; set; }

    private int _xIndex = -1, _yIndex = -1, _zIndex = -1, _locIndex = -1;
    private int _iconIndex = -1, _iconStateIndex = -1, _dirIndex = -1, _alphaIndex = -1;
    private int _colorIndex = -1, _layerIndex = -1, _pixelXIndex = -1, _pixelYIndex = -1;

    private void InitializeBuiltinIndices()
    {
        if (ObjectType == null) return;
        _xIndex = ObjectType.GetVariableIndex("x");
        _yIndex = ObjectType.GetVariableIndex("y");
        _zIndex = ObjectType.GetVariableIndex("z");
        _locIndex = ObjectType.GetVariableIndex("loc");
        _iconIndex = ObjectType.GetVariableIndex("icon");
        _iconStateIndex = ObjectType.GetVariableIndex("icon_state");
        _dirIndex = ObjectType.GetVariableIndex("dir");
        _alphaIndex = ObjectType.GetVariableIndex("alpha");
        _colorIndex = ObjectType.GetVariableIndex("color");
        _layerIndex = ObjectType.GetVariableIndex("layer");
        _pixelXIndex = ObjectType.GetVariableIndex("pixel_x");
        _pixelYIndex = ObjectType.GetVariableIndex("pixel_y");
    }

    private int _x;
    private int _committedX;
    /// <summary>
    /// Gets or sets the X-coordinate of the game object.
    /// </summary>
    public int X
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get { lock (_lock) return _x; }
        set
        {
            int oldX, oldY, oldZ;
            lock (_lock)
            {
                oldX = _x;
                if (oldX == value) return;
                oldY = _y; oldZ = _z;
                _x = value;
                if (_xIndex != -1) _variableValues[_xIndex] = new DreamValue((float)value);
                IncrementVersion();
            }
            PositionChanged?.Invoke(this, oldX, oldY, oldZ);
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
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get { lock (_lock) return _y; }
        set
        {
            int oldX, oldY, oldZ;
            lock (_lock)
            {
                oldY = _y;
                if (oldY == value) return;
                oldX = _x; oldZ = _z;
                _y = value;
                if (_yIndex != -1) _variableValues[_yIndex] = new DreamValue((float)value);
                IncrementVersion();
            }
            PositionChanged?.Invoke(this, oldX, oldY, oldZ);
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
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get { lock (_lock) return _z; }
        set
        {
            int oldX, oldY, oldZ;
            lock (_lock)
            {
                oldZ = _z;
                if (oldZ == value) return;
                oldX = _x; oldY = _y;
                _z = value;
                if (_zIndex != -1) _variableValues[_zIndex] = new DreamValue((float)value);
                IncrementVersion();
            }
            PositionChanged?.Invoke(this, oldX, oldY, oldZ);
        }
    }

    /// <summary>
    /// Gets the committed Z-coordinate, used for consistent reads across threads.
    /// </summary>
    public int CommittedZ => _committedZ;

    private string _icon = string.Empty;
    public string Icon
    {
        get { lock (_lock) return _icon; }
        set { lock (_lock) { if (_icon != value) { _icon = value; if (_iconIndex != -1) _variableValues[_iconIndex] = new DreamValue(value); IncrementVersion(); } } }
    }

    private string _iconState = string.Empty;
    public string IconState
    {
        get { lock (_lock) return _iconState; }
        set { lock (_lock) { if (_iconState != value) { _iconState = value; if (_iconStateIndex != -1) _variableValues[_iconStateIndex] = new DreamValue(value); IncrementVersion(); } } }
    }

    private int _dir = 2;
    public int Dir
    {
        get { lock (_lock) return _dir; }
        set { lock (_lock) { if (_dir != value) { _dir = value; if (_dirIndex != -1) _variableValues[_dirIndex] = new DreamValue((float)value); IncrementVersion(); } } }
    }

    private float _alpha = 255.0f;
    public float Alpha
    {
        get { lock (_lock) return _alpha; }
        set { lock (_lock) { if (_alpha != value) { _alpha = value; if (_alphaIndex != -1) _variableValues[_alphaIndex] = new DreamValue(value); IncrementVersion(); } } }
    }

    private string _color = "#ffffff";
    public string Color
    {
        get { lock (_lock) return _color; }
        set { lock (_lock) { if (_color != value) { _color = value; if (_colorIndex != -1) _variableValues[_colorIndex] = new DreamValue(value); IncrementVersion(); } } }
    }

    private float _layer = 2.0f;
    public float Layer
    {
        get { lock (_lock) return _layer; }
        set { lock (_lock) { if (_layer != value) { _layer = value; if (_layerIndex != -1) _variableValues[_layerIndex] = new DreamValue(value); IncrementVersion(); } } }
    }

    private float _pixelX = 0;
    public float PixelX
    {
        get { lock (_lock) return _pixelX; }
        set { lock (_lock) { if (_pixelX != value) { _pixelX = value; if (_pixelXIndex != -1) _variableValues[_pixelXIndex] = new DreamValue(value); IncrementVersion(); } } }
    }

    private float _pixelY = 0;
    public float PixelY
    {
        get { lock (_lock) return _pixelY; }
        set { lock (_lock) { if (_pixelY != value) { _pixelY = value; if (_pixelYIndex != -1) _variableValues[_pixelYIndex] = new DreamValue(value); IncrementVersion(); } } }
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
                    if (_locIndex != -1)
                        _variableValues[_locIndex] = value != null ? new DreamValue((DreamObject)value) : DreamValue.Null;
                    IncrementVersion();
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

    public override DreamValue GetVariable(string name)
    {
        // Fast-path for high-frequency built-in variables to avoid lock and dictionary lookup
        if (name.Length <= 10) // Optimization: only check short names
        {
            switch (name)
            {
                case "x": return new DreamValue((float)_x);
                case "y": return new DreamValue((float)_y);
                case "z": return new DreamValue((float)_z);
                case "icon": return new DreamValue(_icon);
                case "icon_state": return new DreamValue(_iconState);
                case "dir": return new DreamValue((float)_dir);
                case "loc": return _loc != null ? new DreamValue((DreamObject)_loc) : DreamValue.Null;
                case "name":
                    var n = base.GetVariable(name);
                    return !n.IsNull ? n : new DreamValue(ObjectType?.Name ?? "object");
            }
        }

        if (ObjectType != null)
        {
            int index = ObjectType.GetVariableIndex(name);
            if (index != -1) return GetVariable(index);
        }

        return base.GetVariable(name);
    }

    public override void SetVariable(string name, DreamValue value)
    {
        // Fast-path for high-frequency built-in variables to avoid dictionary lookups
        if (name.Length <= 10)
        {
            switch (name)
            {
                case "x": X = (int)value.GetValueAsFloat(); return;
                case "y": Y = (int)value.GetValueAsFloat(); return;
                case "z": Z = (int)value.GetValueAsFloat(); return;
                case "icon": Icon = value.TryGetValue(out string? s) ? s ?? string.Empty : string.Empty; return;
                case "icon_state": IconState = value.TryGetValue(out string? s2) ? s2 ?? string.Empty : string.Empty; return;
                case "dir": Dir = (int)value.GetValueAsFloat(); return;
                case "loc":
                    if (value.TryGetValue(out DreamObject? locObj) && locObj is IGameObject loc) Loc = loc;
                    else Loc = null;
                    return;
            }
        }

        if (ObjectType != null)
        {
            int index = ObjectType.GetVariableIndex(name);
            if (index != -1)
            {
                SetVariableDirect(index, value);
                return;
            }
        }

        base.SetVariable(name, value);
    }

    public override void SetVariableDirect(int index, DreamValue value)
    {
        if (index < 0) return;

        lock (_lock)
        {
            base.SetVariableDirect(index, value);

            // Use the pre-calculated VariableToBuiltin map for O(1) side-effect dispatch
            var builtinMap = ObjectType?.VariableToBuiltin;
            if (builtinMap != null && index < builtinMap.Length)
            {
                var builtin = builtinMap[index];
                if (builtin != (BuiltinVar)255)
                {
                    switch (builtin)
                    {
                        case BuiltinVar.Icon: _icon = value.TryGetValue(out string? s1) ? s1 ?? string.Empty : string.Empty; break;
                        case BuiltinVar.IconState: _iconState = value.TryGetValue(out string? s2) ? s2 ?? string.Empty : string.Empty; break;
                        case BuiltinVar.Dir: _dir = (int)value.GetValueAsFloat(); break;
                        case BuiltinVar.Alpha: _alpha = value.GetValueAsFloat(); break;
                        case BuiltinVar.Color: _color = value.TryGetValue(out string? s3) ? s3 ?? "#ffffff" : "#ffffff"; break;
                        case BuiltinVar.Layer: _layer = value.GetValueAsFloat(); break;
                        case BuiltinVar.PixelX: _pixelX = value.GetValueAsFloat(); break;
                        case BuiltinVar.PixelY: _pixelY = value.GetValueAsFloat(); break;
                    }
                    return;
                }
            }

            // Fallback to manual index checks (for unfinalized types or coordinates/loc)
            if (index == _xIndex) { X = (int)value.GetValueAsFloat(); }
            else if (index == _yIndex) { Y = (int)value.GetValueAsFloat(); }
            else if (index == _zIndex) { Z = (int)value.GetValueAsFloat(); }
            else if (index == _locIndex)
            {
                if (value.TryGetValue(out DreamObject? locObj) && locObj is IGameObject loc)
                    Loc = loc;
                else
                    Loc = null;
            }
        }
    }

    /// <summary>
    /// Gets or sets the ObjectType of the game object.
    /// </summary>
    public override ObjectType? ObjectType
    {
        get => base.ObjectType;
        set
        {
            lock (_lock)
            {
                base.ObjectType = value;
                InitializeBuiltinIndices();
            }
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
        InitializeBuiltinIndices();
    }

    public void Initialize(ObjectType objectType, int x, int y, int z)
    {
        base.Initialize(objectType);
        _x = x;
        _y = y;
        _z = z;
        Id = Interlocked.Increment(ref nextId);
        InitializeBuiltinIndices();
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
        IncrementVersion();
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
        IncrementVersion();
    }

    public T? GetComponent<T>() where T : class, IComponent
    {
        if (Archetype is Archetype arch)
        {
            var components = arch.GetComponentsInternal<T>();
            if (ArchetypeIndex >= 0 && ArchetypeIndex < components.Length)
            {
                return components[ArchetypeIndex];
            }
            return null;
        }
        return _componentManager?.GetComponent<T>(this);
    }

    public IEnumerable<IComponent> GetComponents()
    {
        if (Archetype is Archetype arch)
        {
            // Count components first to avoid over-allocation
            int count = arch._componentArrays.Count;
            var components = new IComponent[count];
            int i = 0;
            foreach (var array in arch._componentArrays.Values)
            {
                var comp = array.Get(ArchetypeIndex);
                if (comp != null) components[i++] = comp;
            }
            if (i < count) System.Array.Resize(ref components, i);
            return components;
        }
        return _componentManager?.GetAllComponents(this) ?? System.Array.Empty<IComponent>();
    }

    public void SendMessage(IComponentMessage message)
    {
        if (Archetype is Archetype arch)
        {
            var targets = message.TargetComponentTypes;
            if (targets != null && targets.Length > 0)
            {
                foreach (var type in targets)
                {
                    if (arch._componentArrays.TryGetValue(type, out var array))
                    {
                        var component = array.Get(ArchetypeIndex);
                        if (component != null && component.Enabled)
                        {
                            component.OnMessage(message);
                        }
                    }
                }
            }
            else
            {
                foreach (var array in arch._componentArrays.Values)
                {
                    var component = array.Get(ArchetypeIndex);
                    if (component != null && component.Enabled)
                    {
                        component.OnMessage(message);
                    }
                }
            }
        }
        else if (_componentManager != null)
        {
            foreach (var component in _componentManager.GetAllComponents(this))
            {
                if (component.Enabled)
                {
                    component.OnMessage(message);
                }
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
        Archetype = null;
        ArchetypeIndex = -1;
        NextInGridCell = null;
        PrevInGridCell = null;
        CurrentGridCellKey = null;

        if (_componentManager != null)
        {
            // We need to notify manager about component removals during reset
            var toRemove = GetComponents().ToList();
            foreach (var component in toRemove)
            {
                _componentManager.RemoveComponent(this, component.GetType());
            }
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
