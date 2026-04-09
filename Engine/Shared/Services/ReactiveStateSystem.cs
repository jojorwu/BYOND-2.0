using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using Shared.Interfaces;
using Shared.Models;
using Shared.Attributes;

using Shared.Buffers;
namespace Shared.Services;

/// <summary>
/// Core engine service that aggregates individual object changes into optimized batches.
/// Transitions from a polling-based dirty object model to a reactive push-based one.
/// </summary>
[EngineService(typeof(IVariableChangeListener))]
public class ReactiveStateSystem : EngineService, IVariableChangeListener, IShrinkable, ITickable
{
    private readonly ConcurrentDictionary<long, DeltaBatch> _activeBatches = new();
    private readonly IDiagnosticBus _diagnosticBus;
    private readonly IArenaAllocator _arena;
    private readonly SharedPool<List<VariableChange>> _changeListPool = new(() => new List<VariableChange>(8));

    public ReactiveStateSystem(IDiagnosticBus diagnosticBus, IArenaAllocator arena)
    {
        _diagnosticBus = diagnosticBus;
        _arena = arena;
    }

    private long _currentTick;

    public void OnVariableChanged(IGameObject owner, int index, in DreamValue value)
    {
        DeltaBatch? batch = null;
        if (owner.LastDeltaBatchTick == _currentTick)
        {
            batch = (DeltaBatch?)owner.LastDeltaBatch;
        }

        if (batch == null)
        {
            batch = _activeBatches.GetOrAdd(owner.Id, static (id, arg) => new DeltaBatch(id, arg), _changeListPool);
            owner.LastDeltaBatch = batch;
            owner.LastDeltaBatchTick = _currentTick;
        }

        batch.AddChange(index, value);
    }

    public void AdvanceTick() => _currentTick++;

    public ValueTask TickAsync()
    {
        AdvanceTick();
        return ValueTask.CompletedTask;
    }

    public int BatchCount => _activeBatches.Count;

    public void ConsumeBatches(IList<DeltaBatch> destination)
    {
        foreach (var pair in _activeBatches)
        {
            destination.Add(pair.Value);
        }
        _activeBatches.Clear();
    }

    public IEnumerable<DeltaBatch> ConsumeBatches()
    {
        var batches = new List<DeltaBatch>(_activeBatches.Count);
        ConsumeBatches(batches);
        return batches;
    }

    public void Shrink()
    {
        foreach (var batch in _activeBatches.Values) batch.Dispose();
        _activeBatches.Clear();
        _arena.Reset();
    }

    public class DeltaBatch : IDisposable
    {
        public long EntityId { get; }
        private List<VariableChange>? _changes;
        private ulong _changeMask; // Fast mask for first 64 properties
        private readonly System.Threading.Lock _lock = new();
        private readonly SharedPool<List<VariableChange>> _pool;

        public DeltaBatch(long entityId, SharedPool<List<VariableChange>> pool)
        {
            EntityId = entityId;
            _pool = pool;
            _changes = _pool.Rent();
            _changeMask = 0;
        }

        public void AddChange(int index, in DreamValue value)
        {
            using (_lock.EnterScope())
            {
                if (_changes == null) return;

                // Fast path for common properties
                if ((uint)index < 64)
                {
                    if ((_changeMask & (1UL << index)) != 0)
                    {
                        var changes = _changes;
                        for (int i = 0; i < changes.Count; i++)
                        {
                            if (changes[i].Index == index)
                            {
                                changes[i] = new VariableChange { Index = index, Value = value };
                                return;
                            }
                        }
                    }
                    _changeMask |= (1UL << index);
                }
                else
                {
                    // Optimization: if we already have a change for this index, update it
                    for (int i = 0; i < _changes.Count; i++)
                    {
                        if (_changes[i].Index == index)
                        {
                            _changes[i] = new VariableChange { Index = index, Value = value };
                            return;
                        }
                    }
                }
                _changes.Add(new VariableChange { Index = index, Value = value });
            }
        }

        public IReadOnlyList<VariableChange> Changes => _changes ?? (IReadOnlyList<VariableChange>)Array.Empty<VariableChange>();

        public void Dispose()
        {
            using (_lock.EnterScope())
            {
                if (_changes != null)
                {
                    _changes.Clear();
                    _pool.Return(_changes);
                    _changes = null;
                    _changeMask = 0;
                }
            }
        }
    }
}
