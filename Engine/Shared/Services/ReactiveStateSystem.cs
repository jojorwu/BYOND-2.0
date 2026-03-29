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

    public ReactiveStateSystem(IDiagnosticBus diagnosticBus)
    {
        _diagnosticBus = diagnosticBus;
    }

    public void OnVariableChanged(IGameObject owner, int index, in DreamValue value)
    {
        var batch = _activeBatches.GetOrAdd(owner.Id, id => new DeltaBatch(id));
        batch.AddChange(index, value);
    }

    public IEnumerable<DeltaBatch> ConsumeBatches()
    {
        var batches = _activeBatches.Values.ToList();
        _activeBatches.Clear();
        return batches;
    }

    public void Shrink()
    {
        _activeBatches.Clear();
    }

    public class DeltaBatch
    {
        public long EntityId { get; }
        private readonly List<VariableChange> _changes = new();
        private readonly System.Threading.Lock _lock = new();

        public DeltaBatch(long entityId)
        {
            EntityId = entityId;
        }

        public void AddChange(int index, in DreamValue value)
        {
            lock (_lock)
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
                _changes.Add(new VariableChange { Index = index, Value = value });
            }
        }

        public IReadOnlyList<VariableChange> Changes => _changes;
    }
}
