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

    [ThreadStatic]
    private static long _threadIdBase;
    [ThreadStatic]
    private static int _threadIdOffset;
    private const int IdBlockSize = 1024;

    public static void EnsureNextId(long id)
    {
        long current;
        do
        {
            current = nextId;
            if (current > id) break;
        } while (Interlocked.CompareExchange(ref nextId, id + 1, current) != current);
    }

    private static long AllocateNextId()
    {
        if (_threadIdOffset <= 0)
        {
            _threadIdBase = Interlocked.Add(ref nextId, IdBlockSize) - IdBlockSize;
            _threadIdOffset = IdBlockSize;
        }
        _threadIdOffset--;
        return _threadIdBase++;
    }

    private IComponentManager? _componentManager;

    public void SetComponentManager(IComponentManager manager) => _componentManager = manager;

    public long Id { get; set; }
    public object? Archetype { get; set; }
    public int ArchetypeIndex { get; set; }
    public List<IScriptThread>? ActiveThreads { get; set; }
    public object? LastDeltaBatch { get; set; }
    public long LastDeltaBatchTick { get; set; }

    private IEngineUpdateListener? _updateListener;
    public void SetUpdateListener(IEngineUpdateListener listener) => _updateListener = listener;

    private int _isDirty;
    private uint _changeMask;

    public GameObjectFields GetChangeMask() => (GameObjectFields)Volatile.Read(ref _changeMask);
    public void ClearChangeMask() => Interlocked.Exchange(ref _changeMask, 0);

    protected override void IncrementVersion()
    {
        base.IncrementVersion();
        if (Interlocked.CompareExchange(ref _isDirty, 1, 0) == 0)
        {
            _updateListener?.OnStateChanged(this);
        }
    }

    private void MarkFieldDirty(GameObjectFields field)
    {
        uint current;
        uint next;
        do
        {
            current = _changeMask;
            next = current | (uint)field;
        } while (Interlocked.CompareExchange(ref _changeMask, next, current) != current);
    }

    public void ClearDirty()
    {
        Interlocked.Exchange(ref _isDirty, 0);
        _lock.EnterWriteLock();
        try
        {
            _variableStore.ClearModified();
        }
        finally
        {
            _lock.ExitWriteLock();
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

    public struct GameObjectRenderState
    {
        public double X;
        public double Y;
        public double Z;
        public float Rotation;
        public double Alpha;
        public double Layer;
        public double PixelX;
        public double PixelY;
    }

    private GameObjectTransformState _transform;
    private CommittedGameObjectState _committedState;
    public GameObjectRenderState RenderState;

    public Robust.Shared.Maths.Vector3l Position
    {
        get
        {
            _lock.EnterReadLock();
            try { return _transform.Position; }
            finally { _lock.ExitReadLock(); }
        }
        set => SetPosition(value.X, value.Y, value.Z);
    }

    public float Rotation
    {
        get
        {
            if (Archetype is Archetype arch && ArchetypeIndex != -1) return arch.GetRotation(ArchetypeIndex);
            return _transform.Rotation;
        }
        set
        {
            if (_transform.Rotation != value)
            {
                _transform.Rotation = value;
                if (Archetype is Archetype arch && ArchetypeIndex != -1) arch.SetRotation(ArchetypeIndex, value);
                MarkFieldDirty(GameObjectFields.Rotation);
                IncrementVersion();
            }
        }
    }

    public double RenderX { get => RenderState.X; set => RenderState.X = value; }
    public double RenderY { get => RenderState.Y; set => RenderState.Y = value; }
    public double RenderZ { get => RenderState.Z; set => RenderState.Z = value; }

    public long X { get => Position.X; set => SetPosition(value, Y, Z); }
    public long Y { get => Position.Y; set => SetPosition(X, value, Z); }
    public long Z { get => Position.Z; set => SetPosition(X, Y, value); }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal long GetXUnsafe() => _transform.Position.X;
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal long GetYUnsafe() => _transform.Position.Y;
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal long GetZUnsafe() => _transform.Position.Z;
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal IGameObject? GetLocUnsafe() => _loc;

    public long CommittedX => _committedState.Transform.Position.X;
    public long CommittedY => _committedState.Transform.Position.Y;
    public long CommittedZ => _committedState.Transform.Position.Z;

    public string CommittedIcon => _committedState.Visuals.Icon ?? string.Empty;
    public string CommittedIconState => _committedState.Visuals.IconState ?? string.Empty;
    public int CommittedDir => _committedState.Visuals.Dir;
    public double CommittedAlpha => _committedState.Visuals.Alpha;
    public string CommittedColor => _committedState.Visuals.Color ?? "#ffffff";
    public double CommittedLayer => _committedState.Visuals.Layer;
    public double CommittedPixelX => _committedState.Visuals.PixelX;
    public double CommittedPixelY => _committedState.Visuals.PixelY;

    public string Icon
    {
        get
        {
            if (Archetype is Archetype arch && ArchetypeIndex != -1) return arch.GetIcon(ArchetypeIndex);
            var idx = ObjectType?.IconIndex ?? -1;
            return idx != -1 ? GetVariable(idx).StringValue : string.Empty;
        }
        set
        {
            var idx = ObjectType?.IconIndex ?? -1;
            if (idx != -1) SetVariableDirect(idx, new DreamValue(value));
        }
    }

    public string IconState
    {
        get
        {
            if (Archetype is Archetype arch && ArchetypeIndex != -1) return arch.GetIconState(ArchetypeIndex);
            var idx = ObjectType?.IconStateIndex ?? -1;
            return idx != -1 ? GetVariable(idx).StringValue : string.Empty;
        }
        set
        {
            var idx = ObjectType?.IconStateIndex ?? -1;
            if (idx != -1) SetVariableDirect(idx, new DreamValue(value));
        }
    }

    public int Dir
    {
        get
        {
            if (Archetype is Archetype arch && ArchetypeIndex != -1) return arch.GetDir(ArchetypeIndex);
            var idx = ObjectType?.DirIndex ?? -1;
            return idx != -1 ? (int)GetVariable(idx).GetValueAsDouble() : 2;
        }
        set
        {
            var idx = ObjectType?.DirIndex ?? -1;
            if (idx != -1) SetVariableDirect(idx, new DreamValue((double)value));
        }
    }

    public double Alpha
    {
        get
        {
            if (Archetype is Archetype arch && ArchetypeIndex != -1) return arch.GetAlpha(ArchetypeIndex);
            var idx = ObjectType?.AlphaIndex ?? -1;
            return idx != -1 ? GetVariable(idx).GetValueAsDouble() : 255.0;
        }
        set
        {
            var idx = ObjectType?.AlphaIndex ?? -1;
            if (idx != -1) SetVariableDirect(idx, new DreamValue(value));
        }
    }

    public string Color
    {
        get
        {
            if (Archetype is Archetype arch && ArchetypeIndex != -1) return arch.GetColor(ArchetypeIndex);
            var idx = ObjectType?.ColorIndex ?? -1;
            return idx != -1 ? GetVariable(idx).StringValue : "#ffffff";
        }
        set
        {
            var idx = ObjectType?.ColorIndex ?? -1;
            if (idx != -1) SetVariableDirect(idx, new DreamValue(value));
        }
    }

    public double Layer
    {
        get
        {
            if (Archetype is Archetype arch && ArchetypeIndex != -1) return arch.GetLayer(ArchetypeIndex);
            var idx = ObjectType?.LayerIndex ?? -1;
            return idx != -1 ? GetVariable(idx).GetValueAsDouble() : 2.0;
        }
        set
        {
            var idx = ObjectType?.LayerIndex ?? -1;
            if (idx != -1) SetVariableDirect(idx, new DreamValue(value));
        }
    }

    public double PixelX
    {
        get
        {
            if (Archetype is Archetype arch && ArchetypeIndex != -1) return arch.GetPixelX(ArchetypeIndex);
            var idx = ObjectType?.PixelXIndex ?? -1;
            return idx != -1 ? GetVariable(idx).GetValueAsDouble() : 0.0;
        }
        set
        {
            var idx = ObjectType?.PixelXIndex ?? -1;
            if (idx != -1) SetVariableDirect(idx, new DreamValue(value));
        }
    }

    public double PixelY
    {
        get
        {
            if (Archetype is Archetype arch && ArchetypeIndex != -1) return arch.GetPixelY(ArchetypeIndex);
            var idx = ObjectType?.PixelYIndex ?? -1;
            return idx != -1 ? GetVariable(idx).GetValueAsDouble() : 0.0;
        }
        set
        {
            var idx = ObjectType?.PixelYIndex ?? -1;
            if (idx != -1) SetVariableDirect(idx, new DreamValue(value));
        }
    }

    public double Opacity
    {
        get
        {
            if (Archetype is Archetype arch && ArchetypeIndex != -1) return arch.GetOpacity(ArchetypeIndex);
            var idx = ObjectType?.OpacityIndex ?? -1;
            return idx != -1 ? GetVariable(idx).GetValueAsDouble() : 0.0;
        }
        set
        {
            var idx = ObjectType?.OpacityIndex ?? -1;
            if (idx != -1) SetVariableDirect(idx, new DreamValue(value));
        }
    }

    private int _densityVal = 1;
    public bool Density
    {
        get => Volatile.Read(ref _densityVal) == 1;
        set { if (Interlocked.Exchange(ref _densityVal, value ? 1 : 0) != (value ? 1 : 0)) IncrementVersion(); }
    }

    private struct CommitVisitor : IVariableVisitor
    {
        public IVariableStore Target;
        public void Visit(int index, in DreamValue value) => Target.Set(index, value);
    }

    public void CommitState()
    {
        _lock.EnterWriteLock();
        try
        {
            if (Interlocked.Exchange(ref _isDirty, 0) == 0) return;

            _committedState.Transform = _transform;

            var type = ObjectType;
            if (type != null)
            {
                _committedState.Visuals = new GameObjectVisualState
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

            var visitor = new CommitVisitor { Target = _committedStore };
            _variableStore.VisitModified(ref visitor);
            _variableStore.ClearModified();
        }
        finally
        {
            _lock.ExitWriteLock();
        }
    }

    private IGameObject? _loc;
    public IGameObject? Loc
    {
        get { _lock.EnterReadLock(); try { return _loc; } finally { _lock.ExitReadLock(); } }
        set => SetLocInternal(value, true);
    }

    private void SetLocInternal(IGameObject? value, bool syncVariable)
    {
        GameObject? oldLoc;
        GameObject? newLoc;

        GameObject? first = null;
        GameObject? second = null;

        _lock.EnterWriteLock();
        try
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
        finally
        {
            _lock.ExitWriteLock();
        }

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

    public virtual IEnumerable<IGameObject> Contents => _contents;

    public virtual void AddContent(IGameObject obj) { if (obj is GameObject gameObj) gameObj.Loc = this; else AddContentInternal(obj); }
    public virtual void RemoveContent(IGameObject obj) { if (obj is GameObject gameObj && gameObj.Loc == this) gameObj.Loc = null; else RemoveContentInternal(obj); }

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
        if (name.Length >= 1 && name.Length <= 10)
        {
            switch (name)
            {
                case "x": return new DreamValue(X);
                case "y": return new DreamValue(Y);
                case "z": return new DreamValue(Z);
                case "loc": return _loc != null ? new DreamValue((DreamObject)_loc) : DreamValue.Null;
            }
        }
        return base.GetVariable(name);
    }

    public override void SetVariable(string name, DreamValue value)
    {
        if (name.Length >= 1 && name.Length <= 10)
        {
            switch (name)
            {
                case "x": X = value.RawLong; return;
                case "y": Y = value.RawLong; return;
                case "z": Z = value.RawLong; return;
                case "loc": Loc = (value.TryGetValue(out DreamObject? locObj) && locObj is IGameObject loc) ? loc : null; return;
            }
        }
        base.SetVariable(name, value);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public override DreamValue GetVariableDirect(int index)
    {
        var type = ObjectType;
        if (type != null)
        {
            if (index == type.XIndex) return new DreamValue(X);
            if (index == type.YIndex) return new DreamValue(Y);
            if (index == type.ZIndex) return new DreamValue(Z);
            if (index == type.LocIndex) { _lock.EnterReadLock(); try { return _loc != null ? new DreamValue((DreamObject)_loc) : DreamValue.Null; } finally { _lock.ExitReadLock(); } }
        }

        _lock.EnterReadLock();
        try
        {
            return _variableStore.Get(index);
        }
        finally
        {
            _lock.ExitReadLock();
        }
    }

    public override void SetVariableDirect(int index, DreamValue value, bool suppressVersion = false)
    {
        if ((uint)index >= 1000000) return;
        _lock.EnterWriteLock();
        try
        {
            // Fast-path for built-in variables to bypass dictionary lookups in OnVariableChanged
            // and eliminate recursive calls to SetPosition (which would re-acquire the lock)
            var type = ObjectType;
            if (type != null)
            {
                if (index == type.XIndex)
                {
                    long val = value.RawLong;
                    if (GetXUnsafe() == val) return;
                    SetPositionInternal(val, GetYUnsafe(), GetZUnsafe(), out long oldX, out _, out _);
                    _variableStore.Set(index, value);
                    if (!suppressVersion) IncrementVersion();
                    _updateListener?.OnPositionChanged(this, oldX, GetYUnsafe(), GetZUnsafe());
                    return;
                }
                if (index == type.YIndex)
                {
                    long val = value.RawLong;
                    if (GetYUnsafe() == val) return;
                    SetPositionInternal(GetXUnsafe(), val, GetZUnsafe(), out _, out long oldY, out _);
                    _variableStore.Set(index, value);
                    if (!suppressVersion) IncrementVersion();
                    _updateListener?.OnPositionChanged(this, GetXUnsafe(), oldY, GetZUnsafe());
                    return;
                }
                if (index == type.ZIndex)
                {
                    long val = value.RawLong;
                    if (GetZUnsafe() == val) return;
                    SetPositionInternal(GetXUnsafe(), GetYUnsafe(), val, out _, out _, out long oldZ);
                    _variableStore.Set(index, value);
                    if (!suppressVersion) IncrementVersion();
                    _updateListener?.OnPositionChanged(this, GetXUnsafe(), GetYUnsafe(), oldZ);
                    return;
                }
                if (index == type.LocIndex)
                {
                    var loc = (value.TryGetValue(out DreamObject? locObj) && locObj is IGameObject l) ? l : null;
                    if (GetLocUnsafe() == loc) return;
                    SetLocInternal(loc, false);
                    _variableStore.Set(index, value);
                    if (!suppressVersion) IncrementVersion();
                    return;
                }
            }

            var current = _variableStore.Get(index);
            if (current.Equals(value)) return;

            _variableStore.Set(index, value);
            if (!suppressVersion) IncrementVersion();
            OnVariableChanged(index, value);
        }
        finally { _lock.ExitWriteLock(); }
    }

    private void OnVariableChanged(int index, in DreamValue value)
    {
        var builtinMap = ObjectType?.VariableToBuiltin;
        if (builtinMap == null || (uint)index >= (uint)builtinMap.Length) return;
        var builtin = builtinMap[index];
        var arch = Archetype as Archetype;
        int archIdx = ArchetypeIndex;

        switch (builtin)
        {
            case BuiltinVar.X: Position = new Robust.Shared.Maths.Vector3l(value.RawLong, Y, Z); MarkFieldDirty(GameObjectFields.PositionX); break;
            case BuiltinVar.Y: Position = new Robust.Shared.Maths.Vector3l(X, value.RawLong, Z); MarkFieldDirty(GameObjectFields.PositionY); break;
            case BuiltinVar.Z: Position = new Robust.Shared.Maths.Vector3l(X, Y, value.RawLong); MarkFieldDirty(GameObjectFields.PositionZ); break;
            case BuiltinVar.Loc: SetLocInternal((value.TryGetValue(out DreamObject? locObj) && locObj is IGameObject loc) ? loc : null, false); break;
            case BuiltinVar.Density: Interlocked.Exchange(ref _densityVal, value.IsFalse() ? 0 : 1); break;
            case BuiltinVar.Dir:
                int dir = (int)value.GetValueAsDouble();
                if (arch != null && archIdx != -1) arch.SetDir(archIdx, dir);
                MarkFieldDirty(GameObjectFields.Dir);
                break;
            case BuiltinVar.Alpha:
                double alpha = value.GetValueAsDouble();
                if (arch != null && archIdx != -1) arch.SetAlpha(archIdx, alpha);
                MarkFieldDirty(GameObjectFields.Alpha);
                break;
            case BuiltinVar.Color:
                string color = value.StringValue;
                if (arch != null && archIdx != -1) arch.SetColor(archIdx, color);
                MarkFieldDirty(GameObjectFields.Color);
                break;
            case BuiltinVar.Layer:
                double layer = value.GetValueAsDouble();
                if (arch != null && archIdx != -1) arch.SetLayer(archIdx, layer);
                MarkFieldDirty(GameObjectFields.Layer);
                break;
            case BuiltinVar.Icon:
                string icon = value.StringValue;
                if (arch != null && archIdx != -1) arch.SetIcon(archIdx, icon);
                MarkFieldDirty(GameObjectFields.Icon);
                break;
            case BuiltinVar.IconState:
                string state = value.StringValue;
                if (arch != null && archIdx != -1) arch.SetIconState(archIdx, state);
                MarkFieldDirty(GameObjectFields.IconState);
                break;
            case BuiltinVar.PixelX:
                double px = value.GetValueAsDouble();
                if (arch != null && archIdx != -1) arch.SetPixelX(archIdx, px);
                MarkFieldDirty(GameObjectFields.PixelX);
                break;
            case BuiltinVar.PixelY:
                double py = value.GetValueAsDouble();
                if (arch != null && archIdx != -1) arch.SetPixelY(archIdx, py);
                MarkFieldDirty(GameObjectFields.PixelY);
                break;
            case BuiltinVar.Opacity:
                double opacity = value.GetValueAsDouble();
                if (arch != null && archIdx != -1) arch.SetOpacity(archIdx, opacity);
                MarkFieldDirty(GameObjectFields.Opacity);
                break;
        }
    }

    public GameObject(ObjectType objectType) : base(objectType) { Id = AllocateNextId(); }
    public GameObject(ObjectType objectType, long x, long y, long z) : this(objectType) { _transform.Position = new Robust.Shared.Maths.Vector3l(x, y, z); }
    public void Initialize(ObjectType objectType, long x, long y, long z) { base.Initialize(objectType); _transform.Position = new Robust.Shared.Maths.Vector3l(x, y, z); Id = AllocateNextId(); }
    [JsonConstructor] public GameObject() : base(null!) { }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal void SetPositionInternal(long x, long y, long z, out long oldX, out long oldY, out long oldZ)
    {
        oldX = _transform.Position.X;
        oldY = _transform.Position.Y;
        oldZ = _transform.Position.Z;

        if (oldX == x && oldY == y && oldZ == z) return;
        if (oldX != x) MarkFieldDirty(GameObjectFields.PositionX);
        if (oldY != y) MarkFieldDirty(GameObjectFields.PositionY);
        if (oldZ != z) MarkFieldDirty(GameObjectFields.PositionZ);
        _transform.Position = new Robust.Shared.Maths.Vector3l(x, y, z);

        if (Archetype is Archetype arch && ArchetypeIndex != -1)
        {
            arch.SetX(ArchetypeIndex, x);
            arch.SetY(ArchetypeIndex, y);
            arch.SetZ(ArchetypeIndex, z);
        }

        IncrementVersion();
    }

    public void SetPosition(long x, long y, long z)
    {
        long oldX, oldY, oldZ;
        _lock.EnterWriteLock();
        try {
            SetPositionInternal(x, y, z, out oldX, out oldY, out oldZ);
        } finally { _lock.ExitWriteLock(); }
        if (oldX != x || oldY != y || oldZ != z)
            _updateListener?.OnPositionChanged(this, oldX, oldY, oldZ);
    }

    public void AddComponent(IComponent component) { _componentManager?.AddComponent(this, component); IncrementVersion(); }
    public void SubscribeToVariables(IVariableChangeListener listener) { if (_variableStore is IObservableVariableStore observable) observable.Subscribe(listener); }
    public void RemoveComponent(System.Type componentType) { _componentManager?.RemoveComponent(this, componentType); IncrementVersion(); }
    public void RemoveComponent<T>() where T : class, IComponent { RemoveComponent(typeof(T)); }

    public T? GetComponent<T>() where T : class, IComponent {
        if (Archetype is Archetype arch) {
            var components = arch.GetComponentsInternal<T>();
            return (ArchetypeIndex >= 0 && ArchetypeIndex < components.Length) ? components[ArchetypeIndex] : null;
        }
        return _componentManager?.GetComponent<T>(this);
    }

    public T? GetComponent<T>(ArchetypeChunk<T> chunk) where T : class, IComponent
    {
        if (ArchetypeIndex < chunk.Offset || ArchetypeIndex >= chunk.Offset + chunk.Count) return null;
        return chunk.Components[ArchetypeIndex];
    }

    public IEnumerable<IComponent> GetComponents() {
        if (Archetype is Archetype arch) {
            var arrays = arch._componentArrays;
            var components = new List<IComponent>();
            for (int i = 0; i < arrays.Length; i++) {
                var array = arrays[i];
                if (array != null) {
                    var comp = array.Get(ArchetypeIndex);
                    if (comp != null) components.Add(comp);
                }
            }
            return components;
        }
        return _componentManager?.GetAllComponents(this) ?? System.Array.Empty<IComponent>();
    }

    public void VisitComponents<T>(ref T visitor) where T : struct, IComponentVisitor, allows ref struct {
        if (Archetype is Archetype arch && ArchetypeIndex != -1) {
            var arrays = arch._componentArrays;
            for (int i = 0; i < arrays.Length; i++) {
                var array = arrays[i];
                if (array != null) {
                    var comp = array.Get(ArchetypeIndex);
                    if (comp != null) visitor.Visit(comp);
                }
            }
        } else {
            var components = _componentManager?.GetAllComponents(this);
            if (components != null) {
                foreach (var comp in components) visitor.Visit(comp);
            }
        }
    }

    public interface IChangeVisitor : IVariableVisitor { }
    public void VisitChanges<T>(ref T visitor) where T : struct, IChangeVisitor, allows ref struct {
        _lock.EnterReadLock(); try { _variableStore.VisitModified(ref visitor); } finally { _lock.ExitReadLock(); }
    }

    public string TypeName => ObjectType?.Name ?? string.Empty;

    private struct DeltaStateVisitor : IVariableVisitor
    {
        public VariableChange[] Changes;
        public int Index;
        public int StoreLength;
        public void Visit(int index, in DreamValue value)
        {
            if (index < StoreLength)
            {
                Changes[Index++] = new VariableChange { Index = index, Value = value };
            }
        }
    }

    public DeltaState GetDeltaState() {
        _lock.EnterReadLock(); try {
            var changes = ArrayPool<VariableChange>.Shared.Rent(Math.Max(128, _variableStore.Length));
            var visitor = new DeltaStateVisitor { Changes = changes, Index = 0, StoreLength = _variableStore.Length };
            _variableStore.VisitModified(ref visitor);
            if (visitor.Index == 0) { ArrayPool<VariableChange>.Shared.Return(changes); return new DeltaState(Id, null, 0); }
            return new DeltaState(Id, changes, visitor.Index, true);
        } finally { _lock.ExitReadLock(); }
    }

    public void SendMessage(IComponentMessage message)
    {
        var components = GetComponents();
        foreach (var component in components)
        {
            if (component.Enabled)
            {
                component.OnMessage(message);
            }
        }
    }

    public virtual void Reset()
    {
        _componentManager?.GetAllComponents(this).ToList().ForEach(c => _componentManager.RemoveComponent(this, c.GetType()));
        StateMachine = null;
        SetLocInternal(null, false);
        _lock.EnterWriteLock();
        try
        {
            _transform = default;
            _committedState = default;
            _densityVal = 1;
            _isDirty = 0;
            _changeMask = 0;
            _variableStore.Dispose();
            _committedStore.Dispose();
        }
        finally { _lock.ExitWriteLock(); }
        Version = 0;
        Archetype = null;
        ArchetypeIndex = -1;
        ActiveThreads = null;
        SpatialGridIndex = -1;
        CurrentGridCellKey = null;
        _updateListener = null;
        ObjectType = null;
    }

    public IVariableStore Variables => _variableStore;
}

public class GameObjectConverter : JsonConverter<GameObject>
{
    public override GameObject Read(ref Utf8JsonReader reader, System.Type typeToConvert, JsonSerializerOptions options)
    {
        using var doc = JsonDocument.ParseValue(ref reader);
        var root = doc.RootElement;
        var obj = new GameObject();
        if (root.TryGetProperty("Id", out var idProp)) obj.Id = idProp.GetInt64();
        return obj;
    }

    public override void Write(Utf8JsonWriter writer, GameObject value, JsonSerializerOptions options)
    {
        writer.WriteStartObject();
        writer.WriteNumber("Id", value.Id);
        writer.WriteString("TypeName", value.TypeName);
        writer.WriteEndObject();
    }
}
