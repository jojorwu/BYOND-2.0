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
    private static long nextId = 1;
    private static readonly object _globalHierarchyLock = new();

    public static void EnsureNextId(long id)
    {
        long current;
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
    public long Id { get; set; }

    public object? Archetype { get; set; }
    public int ArchetypeIndex { get; set; }

    private IEngineUpdateListener? _updateListener;
    public void SetUpdateListener(IEngineUpdateListener listener) => _updateListener = listener;

    private int _isDirty;
    protected override void IncrementVersion()
    {
        base.IncrementVersion();
        if (Interlocked.CompareExchange(ref _isDirty, 1, 0) == 0)
        {
            _updateListener?.OnStateChanged(this);
        }
    }

    public void ClearDirty()
    {
        Interlocked.Exchange(ref _isDirty, 0);
    }

    public IGameObject? NextInGridCell { get; set; }
    public IGameObject? PrevInGridCell { get; set; }
    public (long X, long Y)? CurrentGridCellKey { get; set; }

    private long _x;
    private long _committedX;
    /// <summary>
    /// Gets or sets the X-coordinate of the game object.
    /// </summary>
    public long X
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get => Interlocked.Read(ref _x);
        set
        {
            long oldX, oldY, oldZ;
            lock (_lock)
            {
                oldX = _x;
                if (oldX == value) return;
                oldY = _y; oldZ = _z;
                _x = value;
                var type = ObjectType;
                if (type != null && type.XIndex != -1) _variableValues[type.XIndex] = new DreamValue(value);
                IncrementVersion();
            }
            _updateListener?.OnPositionChanged(this, oldX, oldY, oldZ);
        }
    }

    /// <summary>
    /// Gets the committed X-coordinate, used for consistent reads across threads.
    /// </summary>
    public long CommittedX => Interlocked.Read(ref _committedX);

    private long _y;
    private long _committedY;
    /// <summary>
    /// Gets or sets the Y-coordinate of the game object.
    /// </summary>
    public long Y
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get => Interlocked.Read(ref _y);
        set
        {
            long oldX, oldY, oldZ;
            lock (_lock)
            {
                oldY = _y;
                if (oldY == value) return;
                oldX = _x; oldZ = _z;
                _y = value;
                var type = ObjectType;
                if (type != null && type.YIndex != -1) _variableValues[type.YIndex] = new DreamValue(value);
                IncrementVersion();
            }
            _updateListener?.OnPositionChanged(this, oldX, oldY, oldZ);
        }
    }

    /// <summary>
    /// Gets the committed Y-coordinate, used for consistent reads across threads.
    /// </summary>
    public long CommittedY => Interlocked.Read(ref _committedY);

    private long _z;
    private long _committedZ;
    /// <summary>
    /// Gets or sets the Z-coordinate of the game object.
    /// </summary>
    public long Z
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get => Interlocked.Read(ref _z);
        set
        {
            long oldX, oldY, oldZ;
            lock (_lock)
            {
                oldZ = _z;
                if (oldZ == value) return;
                oldX = _x; oldY = _y;
                _z = value;
                var type = ObjectType;
                if (type != null && type.ZIndex != -1) _variableValues[type.ZIndex] = new DreamValue(value);
                IncrementVersion();
            }
            _updateListener?.OnPositionChanged(this, oldX, oldY, oldZ);
        }
    }

    /// <summary>
    /// Gets the committed Z-coordinate, used for consistent reads across threads.
    /// </summary>
    public long CommittedZ => _committedZ;

    private string _icon = string.Empty;
    public string Icon
    {
        get => Volatile.Read(ref _icon);
        set { lock (_lock) { if (_icon != value) { _icon = value; var idx = ObjectType?.IconIndex ?? -1; if (idx != -1 && idx < _variableValues.Length) _variableValues[idx] = new DreamValue(value); IncrementVersion(); } } }
    }

    private string _iconState = string.Empty;
    public string IconState
    {
        get => Volatile.Read(ref _iconState);
        set { lock (_lock) { if (_iconState != value) { _iconState = value; var idx = ObjectType?.IconStateIndex ?? -1; if (idx != -1 && idx < _variableValues.Length) _variableValues[idx] = new DreamValue(value); IncrementVersion(); } } }
    }

    private int _dir = 2;
    public int Dir
    {
        get => Volatile.Read(ref _dir);
        set { lock (_lock) { if (_dir != value) { _dir = value; var idx = ObjectType?.DirIndex ?? -1; if (idx != -1 && idx < _variableValues.Length) _variableValues[idx] = new DreamValue((double)value); IncrementVersion(); } } }
    }

    private double _alpha = 255.0;
    public double Alpha
    {
        get => Volatile.Read(ref _alpha);
        set { lock (_lock) { if (_alpha != value) { _alpha = value; var idx = ObjectType?.AlphaIndex ?? -1; if (idx != -1 && idx < _variableValues.Length) _variableValues[idx] = new DreamValue(value); IncrementVersion(); } } }
    }

    private string _color = "#ffffff";
    public string Color
    {
        get => Volatile.Read(ref _color);
        set { lock (_lock) { if (_color != value) { _color = value; var idx = ObjectType?.ColorIndex ?? -1; if (idx != -1 && idx < _variableValues.Length) _variableValues[idx] = new DreamValue(value); IncrementVersion(); } } }
    }

    private double _layer = 2.0;
    public double Layer
    {
        get => Volatile.Read(ref _layer);
        set { lock (_lock) { if (_layer != value) { _layer = value; var idx = ObjectType?.LayerIndex ?? -1; if (idx != -1 && idx < _variableValues.Length) _variableValues[idx] = new DreamValue(value); IncrementVersion(); } } }
    }

    private double _pixelX = 0;
    public double PixelX
    {
        get => Volatile.Read(ref _pixelX);
        set { lock (_lock) { if (_pixelX != value) { _pixelX = value; var idx = ObjectType?.PixelXIndex ?? -1; if (idx != -1 && idx < _variableValues.Length) _variableValues[idx] = new DreamValue(value); IncrementVersion(); } } }
    }

    private double _pixelY = 0;
    public double PixelY
    {
        get => Volatile.Read(ref _pixelY);
        set { lock (_lock) { if (_pixelY != value) { _pixelY = value; var idx = ObjectType?.PixelYIndex ?? -1; if (idx != -1 && idx < _variableValues.Length) _variableValues[idx] = new DreamValue(value); IncrementVersion(); } } }
    }

    private double _opacity = 0;
    public double Opacity
    {
        get => Volatile.Read(ref _opacity);
        set { lock (_lock) { if (_opacity != value) { _opacity = value; var idx = ObjectType?.OpacityIndex ?? -1; if (idx != -1 && idx < _variableValues.Length) _variableValues[idx] = new DreamValue(value); IncrementVersion(); } } }
    }

    /// <summary>
    /// Commits the current state to the read-only buffer.
    /// </summary>
    public void CommitState()
    {
        lock (_lock)
        {
            Interlocked.Exchange(ref _committedX, _x);
            Interlocked.Exchange(ref _committedY, _y);
            Interlocked.Exchange(ref _committedZ, _z);
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
                    var idx = ObjectType?.LocIndex ?? -1;
                    if (idx != -1)
                        _variableValues[idx] = value != null ? new DreamValue((DreamObject)value) : DreamValue.Null;
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
                case "x": return new DreamValue(_x);
                case "y": return new DreamValue(_y);
                case "z": return new DreamValue(_z);
                case "icon": return new DreamValue(_icon);
                case "icon_state": return new DreamValue(_iconState);
                case "dir": return new DreamValue((double)_dir);
                case "opacity": return new DreamValue(_opacity);
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
                case "x": X = value.RawLong; return;
                case "y": Y = value.RawLong; return;
                case "z": Z = value.RawLong; return;
                case "icon": Icon = value.TryGetValue(out string? s) ? s ?? string.Empty : string.Empty; return;
                case "icon_state": IconState = value.TryGetValue(out string? s2) ? s2 ?? string.Empty : string.Empty; return;
                case "dir": Dir = (int)value.GetValueAsDouble(); return;
                case "opacity": Opacity = value.GetValueAsDouble(); return;
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

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public override DreamValue GetVariableDirect(int index)
    {
        var builtinMap = ObjectType?.VariableToBuiltin;
        if (builtinMap != null && index >= 0 && index < builtinMap.Length)
        {
            var builtin = builtinMap[index];
            if (builtin != BuiltinVar.None)
            {
                switch (builtin)
                {
                    case BuiltinVar.X: return new DreamValue(_x);
                    case BuiltinVar.Y: return new DreamValue(_y);
                    case BuiltinVar.Z: return new DreamValue(_z);
                    case BuiltinVar.Icon: return new DreamValue(_icon);
                    case BuiltinVar.IconState: return new DreamValue(_iconState);
                    case BuiltinVar.Dir: return new DreamValue((double)_dir);
                    case BuiltinVar.Alpha: return new DreamValue(_alpha);
                    case BuiltinVar.Color: return new DreamValue(_color);
                    case BuiltinVar.Layer: return new DreamValue(_layer);
                    case BuiltinVar.PixelX: return new DreamValue(_pixelX);
                    case BuiltinVar.PixelY: return new DreamValue(_pixelY);
                    case BuiltinVar.Opacity: return new DreamValue(_opacity);
                    case BuiltinVar.Loc: return _loc != null ? new DreamValue((DreamObject)_loc) : DreamValue.Null;
                }
            }
        }
        return base.GetVariableDirect(index);
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
                if (builtin != BuiltinVar.None)
                {
                    switch (builtin)
                    {
                        case BuiltinVar.Icon: _icon = value.TryGetValue(out string? s1) ? s1 ?? string.Empty : string.Empty; break;
                        case BuiltinVar.IconState: _iconState = value.TryGetValue(out string? s2) ? s2 ?? string.Empty : string.Empty; break;
                        case BuiltinVar.Dir: _dir = (int)value.GetValueAsDouble(); break;
                        case BuiltinVar.Alpha: _alpha = value.GetValueAsDouble(); break;
                        case BuiltinVar.Color: _color = value.TryGetValue(out string? s3) ? s3 ?? "#ffffff" : "#ffffff"; break;
                        case BuiltinVar.Layer: _layer = value.GetValueAsDouble(); break;
                        case BuiltinVar.PixelX: _pixelX = value.GetValueAsDouble(); break;
                        case BuiltinVar.PixelY: _pixelY = value.GetValueAsDouble(); break;
                        case BuiltinVar.Opacity: _opacity = value.GetValueAsDouble(); break;
                        case BuiltinVar.X: X = value.RawLong; break;
                        case BuiltinVar.Y: Y = value.RawLong; break;
                        case BuiltinVar.Z: Z = value.RawLong; break;
                        case BuiltinVar.Loc:
                            if (value.TryGetValue(out DreamObject? locObj) && locObj is IGameObject loc)
                                Loc = loc;
                            else
                                Loc = null;
                            break;
                    }
                    return;
                }
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
    }

    public void Initialize(ObjectType objectType, long x, long y, long z)
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
    public GameObject(ObjectType objectType, long x, long y, long z) : this(objectType)
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
    public void SetPosition(long x, long y, long z)
    {
        long oldX, oldY, oldZ;
        lock (_lock)
        {
            oldX = _x;
            oldY = _y;
            oldZ = _z;

            if (oldX == x && oldY == y && oldZ == z) return;

            _x = x;
            _y = y;
            _z = z;

            var type = ObjectType;
            if (type != null)
            {
                if (type.XIndex != -1) _variableValues[type.XIndex] = new DreamValue(x);
                if (type.YIndex != -1) _variableValues[type.YIndex] = new DreamValue(y);
                if (type.ZIndex != -1) _variableValues[type.ZIndex] = new DreamValue(z);
            }
            IncrementVersion();
        }
        _updateListener?.OnPositionChanged(this, oldX, oldY, oldZ);
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
            _icon = string.Empty;
            _iconState = string.Empty;
            _dir = 2;
            _alpha = 255.0;
            _color = "#ffffff";
            _layer = 2.0;
            _pixelX = 0;
            _pixelY = 0;
            _opacity = 0;
            _isDirty = 0;
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

        _updateListener = null;

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
