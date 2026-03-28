using System.Buffers;
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

    public Robust.Shared.Maths.Vector3l? CurrentGridCellKey { get; set; }
    public IStateMachine? StateMachine { get; set; }

    private struct TransformState
    {
        public Robust.Shared.Maths.Vector3l Position;
        public long CommittedX;
        public long CommittedY;
        public long CommittedZ;
    }

    private struct VisualState
    {
        public string Icon;
        public string IconState;
        public int Dir;
        public double Alpha;
        public string Color;
        public double Layer;
        public double PixelX;
        public double PixelY;
        public double Opacity;
    }

    private TransformState _transform;
    private VisualState _committedVisuals;

    public Robust.Shared.Maths.Vector3l Position
    {
        get { using (_lock.EnterScope()) return _transform.Position; }
        set => SetPosition(value.X, value.Y, value.Z);
    }

    /// <summary>
    /// Gets or sets the X-coordinate of the game object.
    /// </summary>
    public long X
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get { using (_lock.EnterScope()) return _transform.Position.X; }
        set => SetPosition(value, Y, Z);
    }

    /// <summary>
    /// Gets the committed X-coordinate, used for consistent reads across threads.
    /// </summary>
    public long CommittedX { [MethodImpl(MethodImplOptions.AggressiveInlining)] get => Volatile.Read(ref _transform.CommittedX); }

    /// <summary>
    /// Gets or sets the Y-coordinate of the game object.
    /// </summary>
    public long Y
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get { using (_lock.EnterScope()) return _transform.Position.Y; }
        set => SetPosition(X, value, Z);
    }

    /// <summary>
    /// Gets the committed Y-coordinate, used for consistent reads across threads.
    /// </summary>
    public long CommittedY { [MethodImpl(MethodImplOptions.AggressiveInlining)] get => Volatile.Read(ref _transform.CommittedY); }

    /// <summary>
    /// Gets or sets the Z-coordinate of the game object.
    /// </summary>
    public long Z
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get { using (_lock.EnterScope()) return _transform.Position.Z; }
        set => SetPosition(X, Y, value);
    }

    /// <summary>
    /// Gets the committed Z-coordinate, used for consistent reads across threads.
    /// </summary>
    public long CommittedZ { [MethodImpl(MethodImplOptions.AggressiveInlining)] get => Volatile.Read(ref _transform.CommittedZ); }

    public string CommittedIcon => _committedVisuals.Icon ?? string.Empty;
    public string CommittedIconState => _committedVisuals.IconState ?? string.Empty;
    public int CommittedDir => _committedVisuals.Dir;
    public double CommittedAlpha => _committedVisuals.Alpha;
    public string CommittedColor => _committedVisuals.Color ?? "#ffffff";
    public double CommittedLayer => _committedVisuals.Layer;
    public double CommittedPixelX => _committedVisuals.PixelX;
    public double CommittedPixelY => _committedVisuals.PixelY;

    public string Icon
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get { var idx = ObjectType?.IconIndex ?? -1; if (idx == -1) return string.Empty; using (_lock.EnterScope()) { return _variableStore.Get(idx).StringValue; } }
        set { var idx = ObjectType?.IconIndex ?? -1; if (idx != -1) SetVariableDirect(idx, new DreamValue(value)); }
    }

    public string IconState
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get { var idx = ObjectType?.IconStateIndex ?? -1; if (idx == -1) return string.Empty; using (_lock.EnterScope()) { return _variableStore.Get(idx).StringValue; } }
        set { var idx = ObjectType?.IconStateIndex ?? -1; if (idx != -1) SetVariableDirect(idx, new DreamValue(value)); }
    }

    public int Dir
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get { var idx = ObjectType?.DirIndex ?? -1; if (idx == -1) return 2; using (_lock.EnterScope()) { return (int)_variableStore.Get(idx).GetValueAsDouble(); } }
        set { var idx = ObjectType?.DirIndex ?? -1; if (idx != -1) SetVariableDirect(idx, new DreamValue((double)value)); }
    }

    public double Alpha
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get { var idx = ObjectType?.AlphaIndex ?? -1; if (idx == -1) return 255.0; using (_lock.EnterScope()) { return _variableStore.Get(idx).GetValueAsDouble(); } }
        set { var idx = ObjectType?.AlphaIndex ?? -1; if (idx != -1) SetVariableDirect(idx, new DreamValue(value)); }
    }

    public string Color
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get { var idx = ObjectType?.ColorIndex ?? -1; if (idx == -1) return "#ffffff"; using (_lock.EnterScope()) { return _variableStore.Get(idx).StringValue; } }
        set { var idx = ObjectType?.ColorIndex ?? -1; if (idx != -1) SetVariableDirect(idx, new DreamValue(value)); }
    }

    public double Layer
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get { var idx = ObjectType?.LayerIndex ?? -1; if (idx == -1) return 2.0; using (_lock.EnterScope()) { return _variableStore.Get(idx).GetValueAsDouble(); } }
        set { var idx = ObjectType?.LayerIndex ?? -1; if (idx != -1) SetVariableDirect(idx, new DreamValue(value)); }
    }

    public double PixelX
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get { var idx = ObjectType?.PixelXIndex ?? -1; if (idx == -1) return 0.0; using (_lock.EnterScope()) { return _variableStore.Get(idx).GetValueAsDouble(); } }
        set { var idx = ObjectType?.PixelXIndex ?? -1; if (idx != -1) SetVariableDirect(idx, new DreamValue(value)); }
    }

    public double PixelY
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get { var idx = ObjectType?.PixelYIndex ?? -1; if (idx == -1) return 0.0; using (_lock.EnterScope()) { return _variableStore.Get(idx).GetValueAsDouble(); } }
        set { var idx = ObjectType?.PixelYIndex ?? -1; if (idx != -1) SetVariableDirect(idx, new DreamValue(value)); }
    }

    public double Opacity
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get { var idx = ObjectType?.OpacityIndex ?? -1; if (idx == -1) return 0.0; using (_lock.EnterScope()) { return _variableStore.Get(idx).GetValueAsDouble(); } }
        set { var idx = ObjectType?.OpacityIndex ?? -1; if (idx != -1) SetVariableDirect(idx, new DreamValue(value)); }
    }

    private int _densityVal = 1;
    public bool Density
    {
        get => Volatile.Read(ref _densityVal) == 1;
        set { if (Interlocked.Exchange(ref _densityVal, value ? 1 : 0) != (value ? 1 : 0)) IncrementVersion(); }
    }

    /// <summary>
    /// Commits the current state to the read-only buffer.
    /// </summary>
    public void CommitState()
    {
        using (_lock.EnterScope())
        {
            if (Interlocked.Exchange(ref _isDirty, 0) == 0) return;

            Volatile.Write(ref _transform.CommittedX, _transform.Position.X);
            Volatile.Write(ref _transform.CommittedY, _transform.Position.Y);
            Volatile.Write(ref _transform.CommittedZ, _transform.Position.Z);

            var type = ObjectType;
            if (type != null)
            {
                _committedVisuals = new VisualState
                {
                    Icon = type.IconIndex != -1 ? _variableStore.Get(type.IconIndex).StringValue : string.Empty,
                    IconState = type.IconStateIndex != -1 ? _variableStore.Get(type.IconStateIndex).StringValue : string.Empty,
                    Dir = type.DirIndex != -1 ? (int)_variableStore.Get(type.DirIndex).GetValueAsDouble() : 2,
                    Alpha = type.AlphaIndex != -1 ? _variableStore.Get(type.AlphaIndex).GetValueAsDouble() : 255.0,
                    Color = type.ColorIndex != -1 ? _variableStore.Get(type.ColorIndex).StringValue : "#ffffff",
                    Layer = type.LayerIndex != -1 ? _variableStore.Get(type.LayerIndex).GetValueAsDouble() : 2.0,
                    PixelX = type.PixelXIndex != -1 ? _variableStore.Get(type.PixelXIndex).GetValueAsDouble() : 0.0,
                    PixelY = type.PixelYIndex != -1 ? _variableStore.Get(type.PixelYIndex).GetValueAsDouble() : 0.0,
                    Opacity = type.OpacityIndex != -1 ? _variableStore.Get(type.OpacityIndex).GetValueAsDouble() : 0.0
                };
            }

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
                case "x": return new DreamValue(X);
                case "y": return new DreamValue(Y);
                case "z": return new DreamValue(Z);
                case "icon": { var idx = ObjectType?.IconIndex ?? -1; if (idx == -1) return DreamValue.Null; using (_lock.EnterScope()) { return _variableStore.Get(idx); } }
                case "icon_state": { var idx = ObjectType?.IconStateIndex ?? -1; if (idx == -1) return DreamValue.Null; using (_lock.EnterScope()) { return _variableStore.Get(idx); } }
                case "dir": { var idx = ObjectType?.DirIndex ?? -1; if (idx == -1) return new DreamValue(2.0); using (_lock.EnterScope()) { return _variableStore.Get(idx); } }
                case "opacity": { var idx = ObjectType?.OpacityIndex ?? -1; if (idx == -1) return DreamValue.False; using (_lock.EnterScope()) { return _variableStore.Get(idx); } }
                case "loc": return _loc != null ? new DreamValue((DreamObject)_loc) : DreamValue.Null;
                case "name": { var idx = ObjectType?.NameIndex ?? -1; if (idx == -1) return new DreamValue(ObjectType?.Name ?? "object"); using (_lock.EnterScope()) { return _variableStore.Get(idx); } }
                case "desc": { var idx = ObjectType?.DescIndex ?? -1; if (idx == -1) return DreamValue.Null; using (_lock.EnterScope()) { return _variableStore.Get(idx); } }
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
                case "x": SetPosition(value.RawLong, Y, Z); return;
                case "y": SetPosition(X, value.RawLong, Z); return;
                case "z": SetPosition(X, Y, value.RawLong); return;
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
        if (builtinMap != null && (uint)index < (uint)builtinMap.Length)
        {
            var builtin = builtinMap[index];
            if (builtin != BuiltinVar.None)
            {
                switch (builtin)
                {
                    case BuiltinVar.X: return new DreamValue(_transform.Position.X);
                    case BuiltinVar.Y: return new DreamValue(_transform.Position.Y);
                    case BuiltinVar.Z: return new DreamValue(_transform.Position.Z);
                    case BuiltinVar.Loc: return _loc != null ? new DreamValue((DreamObject)_loc) : DreamValue.Null;
                    // Visual properties are stored in _variableStore, so they fall through to base.GetVariableDirect
                }
            }
        }
        return base.GetVariableDirect(index);
    }

    public override void SetVariableDirect(int index, DreamValue value, bool suppressVersion = false)
    {
        if ((uint)index >= 1000000) return;

        bool posChanged = false;
        long oldX = 0, oldY = 0, oldZ = 0;

        using (_lock.EnterScope())
        {
            var current = _variableStore.Get(index);
            bool valueChanged = !current.Equals(value);
            if (!valueChanged) return;

            _variableStore.Set(index, value);
            _changeMask.Set(index);
            if (!suppressVersion) IncrementVersion();

            OnVariableChanged(index, value, ref posChanged, ref oldX, ref oldY, ref oldZ);

            var binding = _bindingService;
            if (binding != null)
            {
                binding.NotifyPropertyChanged(this, index, value);
            }
        }

        if (posChanged)
        {
            _updateListener?.OnPositionChanged(this, oldX, oldY, oldZ);
        }
    }

    private void OnVariableChanged(int index, in DreamValue value, ref bool posChanged, ref long oldX, ref long oldY, ref long oldZ)
    {
        var builtinMap = ObjectType?.VariableToBuiltin;
        if (builtinMap == null || (uint)index >= (uint)builtinMap.Length) return;

        var builtin = builtinMap[index];
        if (builtin == BuiltinVar.None) return;

        switch (builtin)
        {
            case BuiltinVar.X:
                if (_transform.Position.X != value.RawLong)
                {
                    oldX = _transform.Position.X; oldY = _transform.Position.Y; oldZ = _transform.Position.Z;
                    _transform.Position.X = value.RawLong;
                    posChanged = true;
                }
                break;
            case BuiltinVar.Y:
                if (_transform.Position.Y != value.RawLong)
                {
                    oldX = _transform.Position.X; oldY = _transform.Position.Y; oldZ = _transform.Position.Z;
                    _transform.Position.Y = value.RawLong;
                    posChanged = true;
                }
                break;
            case BuiltinVar.Z:
                if (_transform.Position.Z != value.RawLong)
                {
                    oldX = _transform.Position.X; oldY = _transform.Position.Y; oldZ = _transform.Position.Z;
                    _transform.Position.Z = value.RawLong;
                    posChanged = true;
                }
                break;
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
        _transform.Position = new Robust.Shared.Maths.Vector3l(x, y, z);
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
        using (_lock.EnterScope())
        {
            oldX = _transform.Position.X;
            oldY = _transform.Position.Y;
            oldZ = _transform.Position.Z;

            if (oldX == x && oldY == y && oldZ == z) return;

            _transform.Position = new Robust.Shared.Maths.Vector3l(x, y, z);

            var type = ObjectType;
            if (type != null)
            {
                if (type.XIndex != -1)
                {
                    _variableStore.Set(type.XIndex, new DreamValue(x));
                    _changeMask.Set(type.XIndex);
                }
                if (type.YIndex != -1)
                {
                    _variableStore.Set(type.YIndex, new DreamValue(y));
                    _changeMask.Set(type.YIndex);
                }
                if (type.ZIndex != -1)
                {
                    _variableStore.Set(type.ZIndex, new DreamValue(z));
                    _changeMask.Set(type.ZIndex);
                }
            }

            // Increment version once for the whole batched position update
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

    public interface IChangeVisitor : IVariableVisitor
    {
    }

    /// <summary>
    /// Zero-allocation iteration over changed variables using a struct-based visitor.
    /// Utilizes C# 13 'allows ref struct' constraint for peak efficiency.
    /// </summary>
    public void VisitChanges<T>(ref T visitor) where T : struct, IChangeVisitor, allows ref struct
    {
        using (_lock.EnterScope())
        {
            _variableStore.VisitModified(ref visitor);
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

            // Delta tracking optimization: only capture variables modified since last clear/commit
            int count = _changeMask.Count;
            var changes = ArrayPool<VariableChange>.Shared.Rent(count);
            int idx = 0;
            var bits = _changeMask.GetSetBits();
            while (bits.MoveNext())
            {
                int i = bits.Current;
                if (i < _variableStore.Length)
                {
                    changes[idx++] = new VariableChange { Index = i, Value = _variableStore.Get(i) };
                }
            }

            return new DeltaState(Id, changes, idx, true);
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
            // Use the most direct way to notify component manager of removal
            // while iterating over components safely.
            foreach (var component in GetComponents().ToList())
            {
                _componentManager.RemoveComponent(this, component.GetType());
            }
        }

        StateMachine = null;

        SetLocInternal(null, false);
        using (_lock.EnterScope())
        {
            _transform = default;
            _committedVisuals = default;
            _densityVal = 1;
            _isDirty = 0;
            _changeMask.Clear();

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

public class GameObjectConverter : JsonConverter<GameObject>
{
    public override GameObject Read(ref Utf8JsonReader reader, System.Type typeToConvert, JsonSerializerOptions options)
    {
        using var doc = JsonDocument.ParseValue(ref reader);
        var root = doc.RootElement;

        var obj = new GameObject();
        if (root.TryGetProperty("Id", out var idProp)) obj.Id = idProp.GetInt64();

        // Note: Full reconstruction requires access to IObjectTypeManager and GameState
        // This is primarily for DTO-style transfers for now.

        if (root.TryGetProperty("Properties", out var props))
        {
            foreach (var prop in props.EnumerateObject())
            {
                // We should ideally deserialize into DreamValue here
            }
        }

        return obj;
    }

    public override void Write(Utf8JsonWriter writer, GameObject value, JsonSerializerOptions options)
    {
        writer.WriteStartObject();
        writer.WriteNumber("Id", value.Id);
        writer.WriteString("TypeName", value.TypeName);

        writer.WriteStartObject("Transform");
        writer.WriteNumber("X", value.CommittedX);
        writer.WriteNumber("Y", value.CommittedY);
        writer.WriteNumber("Z", value.CommittedZ);
        writer.WriteNumber("Dir", value.Dir);
        writer.WriteEndObject();

        writer.WriteStartObject("Visuals");
        writer.WriteString("Icon", value.Icon);
        writer.WriteString("IconState", value.IconState);
        writer.WriteString("Color", value.Color);
        writer.WriteNumber("Alpha", value.Alpha);
        writer.WriteNumber("Layer", value.Layer);
        writer.WriteEndObject();

        writer.WriteStartObject("Properties");
        if (value.ObjectType != null)
        {
            for (int i = 0; i < value.ObjectType.VariableNames.Count; i++)
            {
                var name = value.ObjectType.VariableNames[i];
                // Skip built-ins already serialized above
                if (name == "x" || name == "y" || name == "z" || name == "dir" ||
                    name == "icon" || name == "icon_state" || name == "color" ||
                    name == "alpha" || name == "layer") continue;

                var val = value.GetVariable(i);
                writer.WritePropertyName(name);
                val.WriteTo(writer, options);
            }
        }
        writer.WriteEndObject();
        writer.WriteEndObject();
    }
}
