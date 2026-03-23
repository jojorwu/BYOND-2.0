using System.Collections.Generic;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Linq;
using Shared.Interfaces;
using Shared.Enums;
using Shared.Models;
using Shared.Serialization;

namespace Shared;

public struct TransformState
{
    public long X;
    public long Y;
    public long Z;
    public int Dir;
}

public struct VisualState
{
    public string Icon;
    public string IconState;
    public string Color;
    public double Alpha;
    public double Layer;
    public double PixelX;
    public double PixelY;
    public double Opacity;
}

/// <summary>
/// Represents an object in the game world.
/// </summary>
[JsonConverter(typeof(GameObjectConverter))]
public class GameObject : DreamObject, IGameObject, IPoolable
{
    private static long nextId = 1;

    public TransformState Transform;
    public VisualState Visuals;

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

    public List<IScriptThread>? ActiveThreads { get; set; }

    private IEngineUpdateListener? _updateListener;
    public void SetUpdateListener(IEngineUpdateListener listener) => _updateListener = listener;

    private int _isDirty;
    private ComponentMask _changeMask = new();

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
        using (_lock.EnterScope())
        {
            _changeMask.Clear();
        }
    }

    private int _spatialGridIndex = -1;
    public int SpatialGridIndex
    {
        get => Volatile.Read(ref _spatialGridIndex);
        set => Interlocked.Exchange(ref _spatialGridIndex, value);
    }

    public (long X, long Y, long Z)? CurrentGridCellKey { get; set; }
    public IStateMachine? StateMachine { get; set; }

    private long _x;
    private long _committedX;
    /// <summary>
    /// Gets or sets the X-coordinate of the game object.
    /// </summary>
    public long X
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get => Interlocked.Read(ref _x);
        set { var idx = ObjectType?.XIndex ?? -1; if (idx != -1) SetVariableDirect(idx, new DreamValue(value)); else SetPosition(value, _y, _z); }
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
        set { var idx = ObjectType?.YIndex ?? -1; if (idx != -1) SetVariableDirect(idx, new DreamValue(value)); else SetPosition(_x, value, _z); }
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
        set { var idx = ObjectType?.ZIndex ?? -1; if (idx != -1) SetVariableDirect(idx, new DreamValue(value)); else SetPosition(_x, _y, value); }
    }

    /// <summary>
    /// Gets the committed Z-coordinate, used for consistent reads across threads.
    /// </summary>
    public long CommittedZ => _committedZ;

    public string CommittedIcon => ObjectType is { IconIndex: var idx and not -1 } ? _committedStore.Get(idx).StringValue : string.Empty;
    public string CommittedIconState => ObjectType is { IconStateIndex: var idx and not -1 } ? _committedStore.Get(idx).StringValue : string.Empty;
    public int CommittedDir => ObjectType is { DirIndex: var idx and not -1 } ? (int)_committedStore.Get(idx).GetValueAsDouble() : 2;
    public double CommittedAlpha => ObjectType is { AlphaIndex: var idx and not -1 } ? _committedStore.Get(idx).GetValueAsDouble() : 255.0;
    public string CommittedColor => ObjectType is { ColorIndex: var idx and not -1 } ? _committedStore.Get(idx).StringValue : "#ffffff";
    public double CommittedLayer => ObjectType is { LayerIndex: var idx and not -1 } ? _committedStore.Get(idx).GetValueAsDouble() : 2.0;
    public double CommittedPixelX => ObjectType is { PixelXIndex: var idx and not -1 } ? _committedStore.Get(idx).GetValueAsDouble() : 0.0;
    public double CommittedPixelY => ObjectType is { PixelYIndex: var idx and not -1 } ? _committedStore.Get(idx).GetValueAsDouble() : 0.0;

    public string Icon
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get => Visuals.Icon;
        set { var type = ObjectType; if (type != null) { var idx = type.IconIndex; if (idx != -1) SetVariableDirect(idx, new DreamValue(value)); } }
    }

    public string IconState
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get => Visuals.IconState;
        set { var type = ObjectType; if (type != null) { var idx = type.IconStateIndex; if (idx != -1) SetVariableDirect(idx, new DreamValue(value)); } }
    }

    public int Dir
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get => Transform.Dir;
        set { var type = ObjectType; if (type != null) { var idx = type.DirIndex; if (idx != -1) SetVariableDirect(idx, new DreamValue((double)value)); } }
    }

    public double Alpha
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get => Visuals.Alpha;
        set { var type = ObjectType; if (type != null) { var idx = type.AlphaIndex; if (idx != -1) SetVariableDirect(idx, new DreamValue(value)); } }
    }

    public string Color
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get => Visuals.Color;
        set { var type = ObjectType; if (type != null) { var idx = type.ColorIndex; if (idx != -1) SetVariableDirect(idx, new DreamValue(value)); } }
    }

    public double Layer
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get => Visuals.Layer;
        set { var type = ObjectType; if (type != null) { var idx = type.LayerIndex; if (idx != -1) SetVariableDirect(idx, new DreamValue(value)); } }
    }

    public double PixelX
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get => Visuals.PixelX;
        set { var type = ObjectType; if (type != null) { var idx = type.PixelXIndex; if (idx != -1) SetVariableDirect(idx, new DreamValue(value)); } }
    }

    public double PixelY
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get => Visuals.PixelY;
        set { var type = ObjectType; if (type != null) { var idx = type.PixelYIndex; if (idx != -1) SetVariableDirect(idx, new DreamValue(value)); } }
    }

    public double Opacity
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get => Visuals.Opacity;
        set { var type = ObjectType; if (type != null) { var idx = type.OpacityIndex; if (idx != -1) SetVariableDirect(idx, new DreamValue(value)); } }
    }

    private int _densityVal = 1;
    public bool Density
    {
        get => Volatile.Read(ref _densityVal) == 1;
        set { var idx = ObjectType?.DensityIndex ?? -1; if (idx != -1) SetVariableDirect(idx, value ? DreamValue.True : DreamValue.False); else { if (Interlocked.Exchange(ref _densityVal, value ? 1 : 0) != (value ? 1 : 0)) IncrementVersion(); } }
    }

    /// <summary>
    /// Commits the current state to the read-only buffer.
    /// </summary>
    public void CommitState()
    {
        using (_lock.EnterScope())
        {
            if (Interlocked.Exchange(ref _isDirty, 0) == 0) return;

            Interlocked.Exchange(ref _committedX, _x);
            Interlocked.Exchange(ref _committedY, _y);
            Interlocked.Exchange(ref _committedZ, _z);

            // Optimized commit: only update variables that have actually changed since last commit
            if (!_changeMask.IsEmpty)
            {
                var bits = _changeMask.GetSetBits();
                while (bits.MoveNext())
                {
                    int i = bits.Current;
                    if (i < _variableStore.Length)
                    {
                        _committedStore.Set(i, _variableStore.Get(i));
                    }
                }
                _changeMask.Clear();
            }
        }
    }

    public delegate void ChangeVisitorRef<T>(int index, in DreamValue value, ref T state) where T : allows ref struct;

    public void VisitChangesRef<T>(ref T state, ChangeVisitorRef<T> visitor) where T : allows ref struct
    {
        using (_lock.EnterScope())
        {
            if (_changeMask.IsEmpty) return;

            var bits = _changeMask.GetSetBits();
            while (bits.MoveNext())
            {
                int i = bits.Current;
                if (i < _variableStore.Length)
                {
                    visitor(i, _variableStore.Get(i), ref state);
                }
            }
        }
    }

    private IGameObject? _loc;
    /// <summary>
    /// Gets or sets the location of the game object.
    /// </summary>
    public IGameObject? Loc
    {
        get { using (_lock.EnterScope()) return _loc; }
        set => SetLocInternal(value, true);
    }

    private void SetLocInternal(IGameObject? value, bool syncVariable)
    {
        GameObject? oldLoc;
        GameObject? newLoc;

        // Consistent lock ordering (by ID) to avoid deadlocks
        GameObject? first = null;
        GameObject? second = null;

        using (_lock.EnterScope())
        {
            if (_loc == value) return;
            oldLoc = _loc as GameObject;
            newLoc = value as GameObject;

            if (oldLoc != null && newLoc != null)
            {
                if (oldLoc.Id < newLoc.Id) { first = oldLoc; second = newLoc; }
                else { first = newLoc; second = oldLoc; }
            }
            else if (oldLoc != null) { first = oldLoc; }
            else if (newLoc != null) { first = newLoc; }

            _loc = value;

            if (syncVariable)
            {
                var idx = ObjectType?.LocIndex ?? -1;
                if (idx != -1)
                    _variableStore.Set(idx, value != null ? new DreamValue((DreamObject)value) : DreamValue.Null);
                IncrementVersion();
            }
        }

        // Apply contents changes outside of self-lock, but with ordered container locks
        if (first != null)
        {
            using (first._contentsLock.EnterScope())
            {
                if (second != null)
                {
                    using (second._contentsLock.EnterScope())
                    {
                        if (first == oldLoc) first.RemoveContentInternal(this);
                        else second.RemoveContentInternal(this);

                        if (first == newLoc) first.AddContentInternal(this);
                        else second.AddContentInternal(this);
                    }
                }
                else
                {
                    if (first == oldLoc) first.RemoveContentInternal(this);
                    else first.AddContentInternal(this);
                }
            }
        }
    }

    protected readonly System.Threading.Lock _contentsLock = new();
    protected volatile IGameObject[] _contents = System.Array.Empty<IGameObject>();

    /// <summary>
    /// Gets the contents of this game object.
    /// </summary>
    public virtual IEnumerable<IGameObject> Contents => _contents;

    public virtual void AddContent(IGameObject obj)
    {
        if (obj == null) return;
        if (obj is GameObject gameObj)
        {
            gameObj.Loc = this;
        }
        else
        {
            AddContentInternal(obj);
        }
    }

    public virtual void RemoveContent(IGameObject obj)
    {
        if (obj == null) return;
        if (obj is GameObject gameObj)
        {
            if (gameObj.Loc == this)
            {
                gameObj.Loc = null;
            }
        }
        else
        {
            RemoveContentInternal(obj);
        }
    }

    internal void AddContentInternal(IGameObject obj)
    {
        if (obj == null) return;
        using (_contentsLock.EnterScope())
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
        if (obj == null) return;
        using (_contentsLock.EnterScope())
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
                case "x": return new DreamValue(Interlocked.Read(ref _x));
                case "y": return new DreamValue(Interlocked.Read(ref _y));
                case "z": return new DreamValue(Interlocked.Read(ref _z));
                case "icon": { var idx = ObjectType?.IconIndex ?? -1; return idx != -1 ? GetVariableInternal(idx) : DreamValue.Null; }
                case "icon_state": { var idx = ObjectType?.IconStateIndex ?? -1; return idx != -1 ? GetVariableInternal(idx) : DreamValue.Null; }
                case "dir": { var idx = ObjectType?.DirIndex ?? -1; return idx != -1 ? GetVariableInternal(idx) : new DreamValue(2.0); }
                case "opacity": { var idx = ObjectType?.OpacityIndex ?? -1; return idx != -1 ? GetVariableInternal(idx) : DreamValue.False; }
                case "loc": return _loc != null ? new DreamValue((DreamObject)_loc) : DreamValue.Null;
                case "name": { var idx = ObjectType?.NameIndex ?? -1; return idx != -1 ? GetVariableInternal(idx) : new DreamValue(ObjectType?.Name ?? "object"); }
                case "desc": { var idx = ObjectType?.DescIndex ?? -1; return idx != -1 ? GetVariableInternal(idx) : DreamValue.Null; }
            }
        }

        if (ObjectType != null)
        {
            int index = ObjectType.GetVariableIndex(name);
            if (index != -1) return GetVariableInternal(index);
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
                case "icon": { var idx = ObjectType?.IconIndex ?? -1; if (idx != -1) SetVariableDirect(idx, value); return; }
                case "icon_state": { var idx = ObjectType?.IconStateIndex ?? -1; if (idx != -1) SetVariableDirect(idx, value); return; }
                case "dir": { var idx = ObjectType?.DirIndex ?? -1; if (idx != -1) SetVariableDirect(idx, value); return; }
                case "opacity": { var idx = ObjectType?.OpacityIndex ?? -1; if (idx != -1) SetVariableDirect(idx, value); return; }
                case "name": { var idx = ObjectType?.NameIndex ?? -1; if (idx != -1) SetVariableDirect(idx, value); return; }
                case "desc": { var idx = ObjectType?.DescIndex ?? -1; if (idx != -1) SetVariableDirect(idx, value); return; }
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
                    case BuiltinVar.X: return new DreamValue(Interlocked.Read(ref _x));
                    case BuiltinVar.Y: return new DreamValue(Interlocked.Read(ref _y));
                    case BuiltinVar.Z: return new DreamValue(Interlocked.Read(ref _z));
                    case BuiltinVar.Loc: return _loc != null ? new DreamValue((DreamObject)_loc) : DreamValue.Null;
                    // Visual properties are stored in _variableStore, so they fall through to base.GetVariableDirect
                }
            }
        }
        return base.GetVariableDirect(index);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private void OnVariableChanged(BuiltinVar builtin, DreamValue value, ref long oldX, ref long oldY, ref long oldZ, ref bool posChanged)
    {
        switch (builtin)
        {
            case BuiltinVar.X:
                long vx = value.RawLong;
                if (Interlocked.Read(ref _x) != vx)
                {
                    if (!posChanged) { oldX = _x; oldY = _y; oldZ = _z; }
                    Interlocked.Exchange(ref _x, vx);
                    Transform.X = vx;
                    posChanged = true;
                }
                break;
            case BuiltinVar.Y:
                long vy = value.RawLong;
                if (Interlocked.Read(ref _y) != vy)
                {
                    if (!posChanged) { oldX = _x; oldY = _y; oldZ = _z; }
                    Interlocked.Exchange(ref _y, vy);
                    Transform.Y = vy;
                    posChanged = true;
                }
                break;
            case BuiltinVar.Z:
                long vz = value.RawLong;
                if (Interlocked.Read(ref _z) != vz)
                {
                    if (!posChanged) { oldX = _x; oldY = _y; oldZ = _z; }
                    Interlocked.Exchange(ref _z, vz);
                    Transform.Z = vz;
                    posChanged = true;
                }
                break;
            case BuiltinVar.Dir: Transform.Dir = (int)value.GetValueAsDouble(); break;
            case BuiltinVar.Icon: Visuals.Icon = value.StringValue; break;
            case BuiltinVar.IconState: Visuals.IconState = value.StringValue; break;
            case BuiltinVar.Color: Visuals.Color = value.StringValue; break;
            case BuiltinVar.Alpha: Visuals.Alpha = value.GetValueAsDouble(); break;
            case BuiltinVar.Layer: Visuals.Layer = value.GetValueAsDouble(); break;
            case BuiltinVar.PixelX: Visuals.PixelX = value.GetValueAsDouble(); break;
            case BuiltinVar.PixelY: Visuals.PixelY = value.GetValueAsDouble(); break;
            case BuiltinVar.Opacity: Visuals.Opacity = value.GetValueAsDouble(); break;
            case BuiltinVar.Loc:
                if (value.TryGetValue(out DreamObject? locObj) && locObj is IGameObject loc)
                    SetLocInternal(loc, false);
                else
                    SetLocInternal(null, false);
                break;
            case BuiltinVar.Density:
                Interlocked.Exchange(ref _densityVal, value.IsFalse() ? 0 : 1);
                break;
        }
    }

    public override void SetVariableDirect(int index, DreamValue value, bool suppressVersion = false)
    {
        if (index < 0) return;

        long oldX = -1, oldY = -1, oldZ = -1;
        bool posChanged = false;

        using (_lock.EnterScope())
        {
            var current = _variableStore.Get(index);
            bool valueChanged = !current.Equals(value);
            if (valueChanged)
            {
                _variableStore.Set(index, value);
                _changeMask.Set(index);
                if (!suppressVersion)
                {
                    IncrementVersion();
                }

                // Use the pre-calculated VariableToBuiltin map for O(1) side-effect dispatch
                var builtinMap = ObjectType?.VariableToBuiltin;
                if (builtinMap != null && index < builtinMap.Length)
                {
                    OnVariableChanged(builtinMap[index], value, ref oldX, ref oldY, ref oldZ, ref posChanged);
                }

                _bindingService?.NotifyPropertyChanged(this, index, value);
            }
        }

        if (posChanged)
        {
            _updateListener?.OnPositionChanged(this, oldX, oldY, oldZ);
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
            using (_lock.EnterScope())
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
        long oldX = -1, oldY = -1, oldZ = -1;
        bool posChanged = false;

        using (_lock.EnterScope())
        {
            if (_x == x && _y == y && _z == z) return;

            oldX = _x; oldY = _y; oldZ = _z;

            var type = ObjectType;
            if (type != null)
            {
                if (type.XIndex != -1) { _variableStore.Set(type.XIndex, new DreamValue(x)); _changeMask.Set(type.XIndex); }
                if (type.YIndex != -1) { _variableStore.Set(type.YIndex, new DreamValue(y)); _changeMask.Set(type.YIndex); }
                if (type.ZIndex != -1) { _variableStore.Set(type.ZIndex, new DreamValue(z)); _changeMask.Set(type.ZIndex); }
            }

            _x = x; _y = y; _z = z;
            Transform.X = x; Transform.Y = y; Transform.Z = z;
            posChanged = true;

            IncrementVersion();
        }

        if (posChanged)
        {
            _updateListener?.OnPositionChanged(this, oldX, oldY, oldZ);
        }
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
            var arrays = arch._componentArrays;
            int count = arrays.Length;
            var components = new List<IComponent>();
            for (int i = 0; i < count; i++)
            {
                var array = arrays[i];
                if (array != null)
                {
                    var comp = array.Get(ArchetypeIndex);
                    if (comp != null) components.Add(comp);
                }
            }
            return components;
        }
        return _componentManager?.GetAllComponents(this) ?? System.Array.Empty<IComponent>();
    }

    public interface IChangeVisitor
    {
        void Visit(int index, in DreamValue value);
    }

    /// <summary>
    /// Zero-allocation iteration over changed variables using a struct-based visitor.
    /// Utilizes C# 13 'allows ref struct' constraint for peak efficiency.
    /// </summary>
    public void VisitChanges<T>(ref T visitor) where T : struct, IChangeVisitor, allows ref struct
    {
        using (_lock.EnterScope())
        {
            if (_changeMask.IsEmpty) return;

            var bits = _changeMask.GetSetBits();
            while (bits.MoveNext())
            {
                int i = bits.Current;
                if (i < _variableStore.Length)
                {
                    visitor.Visit(i, _variableStore.Get(i));
                }
            }
        }
    }


    public DeltaState GetDeltaState()
    {
        using (_lock.EnterScope())
        {
            if (_changeMask.IsEmpty)
            {
                return new DeltaState(Id, null, 0);
            }

            int count = _changeMask.Count;
            var changes = new VariableChange[count];
            int idx = 0;
            foreach (int i in _changeMask.GetSetBits())
            {
                if (i < _variableStore.Length)
                {
                    changes[idx++] = new VariableChange { Index = i, Value = _variableStore.Get(i) };
                }
            }

            return new DeltaState(Id, changes, idx);
        }
    }

    public void SendMessage(IComponentMessage message)
    {
        if (Archetype is Archetype arch)
        {
            var targets = message.TargetComponentTypes;
            var arrays = arch._componentArrays;
            if (targets != null && targets.Length > 0)
            {
                foreach (var type in targets)
                {
                    int id = Services.ComponentIdRegistry.GetId(type);
                    if (id < arrays.Length)
                    {
                        var array = arrays[id];
                        if (array != null)
                        {
                            var component = array.Get(ArchetypeIndex);
                            if (component != null && component.Enabled)
                            {
                                component.OnMessage(message);
                            }
                        }
                    }
                }
            }
            else
            {
                for (int i = 0; i < arrays.Length; i++)
                {
                    var array = arrays[i];
                    if (array != null)
                    {
                        var component = array.Get(ArchetypeIndex);
                        if (component != null && component.Enabled)
                        {
                            component.OnMessage(message);
                        }
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
        if (_componentManager != null)
        {
            var toRemove = GetComponents().ToList();
            foreach (var component in toRemove)
            {
                _componentManager.RemoveComponent(this, component.GetType());
            }
        }

        StateMachine = null;

        SetLocInternal(null, false);
        using (_lock.EnterScope())
        {
            Interlocked.Exchange(ref _x, 0);
            Interlocked.Exchange(ref _y, 0);
            Interlocked.Exchange(ref _z, 0);
            Interlocked.Exchange(ref _committedX, 0);
            Interlocked.Exchange(ref _committedY, 0);
            Interlocked.Exchange(ref _committedZ, 0);
            _densityVal = 1;
            _isDirty = 0;

            _variableStore.Dispose();
            _committedStore.Dispose();
        }
        Version = 0;
        Archetype = null;
        ArchetypeIndex = -1;
        ActiveThreads = null;
        SpatialGridIndex = -1;
        CurrentGridCellKey = null;

        using (_contentsLock.EnterScope())
        {
            _contents = System.Array.Empty<IGameObject>();
        }

        _updateListener = null;
    }
}

