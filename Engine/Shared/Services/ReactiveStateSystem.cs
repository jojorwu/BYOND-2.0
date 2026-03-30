using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using Shared.Interfaces;
using Shared.Models;

namespace Shared.Services;

/// <summary>
/// Core engine service that aggregates individual object changes into optimized batches.
/// Transitions from a polling-based dirty object model to a reactive push-based one.
/// </summary>
public class ReactiveStateSystem : EngineService, IVariableChangeListener, IShrinkable
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

    public void OnVariableChanged(IGameObject owner, int index, in DreamValue value)
    {
        var batch = _activeBatches.GetOrAdd(owner.Id, id => {
            return new DeltaBatch(id, _changeListPool);
        });
        batch.AddChange(index, value);
    }

    public IEnumerable<DeltaBatch> ConsumeBatches()
    {
        // Snapshot current batches
        var batches = _activeBatches.Values.ToList();
        _activeBatches.Clear();
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
        private readonly System.Threading.Lock _lock = new();
        private readonly SharedPool<List<VariableChange>> _pool;

        public DeltaBatch(long entityId, SharedPool<List<VariableChange>> pool)
        {
            EntityId = entityId;
            _pool = pool;
            _changes = _pool.Rent();
        }

        public void AddChange(int index, in DreamValue value)
        {
            using (_lock.EnterScope())
            {
                if (_changes == null) return;

                // Optimization: if we already have a change for this index, update it
                for (int i = 0; i < _changes.Count; i++)
                {
                    if (_changes[i].Index == index)
                    {
                        _changes[i] = new VariableChange { Index = index, Value = value };
                        return;
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
                }
            }
        }
    }
}
