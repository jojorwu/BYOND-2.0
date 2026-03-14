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
        lock (_lock)
        {
            _changeMask = new ComponentMask();
        }
    }

    public IGameObject? NextInGridCell { get; set; }
    public IGameObject? PrevInGridCell { get; set; }
    public (long X, long Y)? CurrentGridCellKey { get; set; }
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
                if (type != null && type.XIndex != -1)
                {
                    _variableStore.Set(type.XIndex, new DreamValue(value));
                    _changeMask.Set(type.XIndex);
                }
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
                if (type != null && type.YIndex != -1)
                {
                    _variableStore.Set(type.YIndex, new DreamValue(value));
                    _changeMask.Set(type.YIndex);
                }
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
                if (type != null && type.ZIndex != -1)
                {
                    _variableStore.Set(type.ZIndex, new DreamValue(value));
                    _changeMask.Set(type.ZIndex);
                }
                IncrementVersion();
            }
            _updateListener?.OnPositionChanged(this, oldX, oldY, oldZ);
        }
    }

    /// <summary>
    /// Gets the committed Z-coordinate, used for consistent reads across threads.
    /// </summary>
    public long CommittedZ => _committedZ;

    public string Icon
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get { var idx = ObjectType?.IconIndex ?? -1; return idx != -1 ? _variableStore.Get(idx).StringValue : string.Empty; }
        set { var idx = ObjectType?.IconIndex ?? -1; if (idx != -1) SetVariableDirect(idx, new DreamValue(value)); }
    }

    public string IconState
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get { var idx = ObjectType?.IconStateIndex ?? -1; return idx != -1 ? _variableStore.Get(idx).StringValue : string.Empty; }
        set { var idx = ObjectType?.IconStateIndex ?? -1; if (idx != -1) SetVariableDirect(idx, new DreamValue(value)); }
    }

    public int Dir
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get { var idx = ObjectType?.DirIndex ?? -1; return idx != -1 ? (int)_variableStore.Get(idx).GetValueAsDouble() : 2; }
        set { var idx = ObjectType?.DirIndex ?? -1; if (idx != -1) SetVariableDirect(idx, new DreamValue((double)value)); }
    }

    public double Alpha
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get { var idx = ObjectType?.AlphaIndex ?? -1; return idx != -1 ? _variableStore.Get(idx).GetValueAsDouble() : 255.0; }
        set { var idx = ObjectType?.AlphaIndex ?? -1; if (idx != -1) SetVariableDirect(idx, new DreamValue(value)); }
    }

    public string Color
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get { var idx = ObjectType?.ColorIndex ?? -1; return idx != -1 ? _variableStore.Get(idx).StringValue : "#ffffff"; }
        set { var idx = ObjectType?.ColorIndex ?? -1; if (idx != -1) SetVariableDirect(idx, new DreamValue(value)); }
    }

    public double Layer
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get { var idx = ObjectType?.LayerIndex ?? -1; return idx != -1 ? _variableStore.Get(idx).GetValueAsDouble() : 2.0; }
        set { var idx = ObjectType?.LayerIndex ?? -1; if (idx != -1) SetVariableDirect(idx, new DreamValue(value)); }
    }

    public double PixelX
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get { var idx = ObjectType?.PixelXIndex ?? -1; return idx != -1 ? _variableStore.Get(idx).GetValueAsDouble() : 0.0; }
        set { var idx = ObjectType?.PixelXIndex ?? -1; if (idx != -1) SetVariableDirect(idx, new DreamValue(value)); }
    }

    public double PixelY
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get { var idx = ObjectType?.PixelYIndex ?? -1; return idx != -1 ? _variableStore.Get(idx).GetValueAsDouble() : 0.0; }
        set { var idx = ObjectType?.PixelYIndex ?? -1; if (idx != -1) SetVariableDirect(idx, new DreamValue(value)); }
    }

    public double Opacity
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get { var idx = ObjectType?.OpacityIndex ?? -1; return idx != -1 ? _variableStore.Get(idx).GetValueAsDouble() : 0.0; }
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
        lock (_lock)
        {
            Interlocked.Exchange(ref _committedX, _x);
            Interlocked.Exchange(ref _committedY, _y);
            Interlocked.Exchange(ref _committedZ, _z);

            // Optimized commit: only update variables that have actually changed since last commit
            if (!_changeMask.IsEmpty)
            {
                foreach (int i in _changeMask.GetSetBits())
                {
                    if (i < _variableStore.Length)
                    {
                        _committedStore.Set(i, _variableStore.Get(i));
                    }
                }
            }
        }
    }

    public delegate void ChangeVisitorRef<T>(int index, in DreamValue value, ref T state) where T : allows ref struct;

    public void VisitChangesRef<T>(ref T state, ChangeVisitorRef<T> visitor) where T : allows ref struct
    {
        lock (_lock)
        {
            if (_changeMask.IsEmpty) return;

            foreach (int i in _changeMask.GetSetBits())
            {
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
        get { lock (_lock) return _loc; }
        set => SetLocInternal(value, true);
    }

    private void SetLocInternal(IGameObject? value, bool syncVariable)
    {
        GameObject? oldLoc;
        GameObject? newLoc;

        // Consistent lock ordering (by ID) to avoid deadlocks
        GameObject? first = null;
        GameObject? second = null;

        lock (_lock)
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
            lock (first._contentsLock)
            {
                if (second != null)
                {
                    lock (second._contentsLock)
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
            AddContentInternal(obj);
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
            RemoveContentInternal(obj);
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
                case "x": return new DreamValue(Interlocked.Read(ref _x));
                case "y": return new DreamValue(Interlocked.Read(ref _y));
                case "z": return new DreamValue(Interlocked.Read(ref _z));
                case "icon": { var idx = ObjectType?.IconIndex ?? -1; return idx != -1 ? _variableStore.Get(idx) : DreamValue.Null; }
                case "icon_state": { var idx = ObjectType?.IconStateIndex ?? -1; return idx != -1 ? _variableStore.Get(idx) : DreamValue.Null; }
                case "dir": { var idx = ObjectType?.DirIndex ?? -1; return idx != -1 ? _variableStore.Get(idx) : new DreamValue(2.0); }
                case "opacity": { var idx = ObjectType?.OpacityIndex ?? -1; return idx != -1 ? _variableStore.Get(idx) : DreamValue.False; }
                case "loc": return _loc != null ? new DreamValue((DreamObject)_loc) : DreamValue.Null;
                case "name": { var idx = ObjectType?.NameIndex ?? -1; return idx != -1 ? _variableStore.Get(idx) : new DreamValue(ObjectType?.Name ?? "object"); }
                case "desc": { var idx = ObjectType?.DescIndex ?? -1; return idx != -1 ? _variableStore.Get(idx) : DreamValue.Null; }
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

    public override void SetVariableDirect(int index, DreamValue value, bool suppressVersion = false)
    {
        if (index < 0) return;

        long oldX = -1, oldY = -1, oldZ = -1;
        bool posChanged = false;

        lock (_lock)
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
            }

            // Use the pre-calculated VariableToBuiltin map for O(1) side-effect dispatch
            var builtinMap = ObjectType?.VariableToBuiltin;
            if (builtinMap != null && index < builtinMap.Length)
            {
                var builtin = builtinMap[index];
                if (builtin != BuiltinVar.None)
                {
                    switch (builtin)
                    {
                        case BuiltinVar.X:
                            if (Interlocked.Read(ref _x) != value.RawLong)
                            {
                                oldX = _x; oldY = _y; oldZ = _z;
                                Interlocked.Exchange(ref _x, value.RawLong);
                                posChanged = true;
                            }
                            break;
                        case BuiltinVar.Y:
                            if (Interlocked.Read(ref _y) != value.RawLong)
                            {
                                oldX = _x; oldY = _y; oldZ = _z;
                                Interlocked.Exchange(ref _y, value.RawLong);
                                posChanged = true;
                            }
                            break;
                        case BuiltinVar.Z:
                            if (Interlocked.Read(ref _z) != value.RawLong)
                            {
                                oldX = _x; oldY = _y; oldZ = _z;
                                Interlocked.Exchange(ref _z, value.RawLong);
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
            }

            if (valueChanged)
            {
                var binding = _bindingService;
                if (binding != null)
                {
                    binding.NotifyPropertyChanged(this, index, value);
                }
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

    public interface IChangeVisitor
    {
        void Visit(int index, in DreamValue value);
    }

    public void VisitChanges<T>(ref T visitor) where T : struct, IChangeVisitor, allows ref struct
    {
        lock (_lock)
        {
            if (_changeMask.IsEmpty) return;

            foreach (int i in _changeMask.GetSetBits())
            {
                if (i < _variableStore.Length)
                {
                    visitor.Visit(i, _variableStore.Get(i));
                }
            }
        }
    }

    public DeltaState GetDeltaState()
    {
        lock (_lock)
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
        lock (_lock)
        {
            Interlocked.Exchange(ref _x, 0);
            Interlocked.Exchange(ref _y, 0);
            Interlocked.Exchange(ref _z, 0);
            Interlocked.Exchange(ref _committedX, 0);
            Interlocked.Exchange(ref _committedY, 0);
            Interlocked.Exchange(ref _committedZ, 0);
            _densityVal = 1;
            _isDirty = 0;
        }
        Version = 0;
        Archetype = null;
        ArchetypeIndex = -1;
        ActiveThreads = null;
        NextInGridCell = null;
        PrevInGridCell = null;
        CurrentGridCellKey = null;

        lock (_contentsLock)
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
        writer.WriteNumber("X", value.X);
        writer.WriteNumber("Y", value.Y);
        writer.WriteNumber("Z", value.Z);
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
