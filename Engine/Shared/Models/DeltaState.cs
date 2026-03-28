using System.Collections.Generic;

namespace Shared.Models;

/// <summary>
/// Represents a set of changes to a game object's state.
/// Used for efficient network synchronization.
/// </summary>
public struct VariableChange
{
    public int Index;
    public DreamValue Value;
}

/// <summary>
/// Represents a set of changes to a game object's state.
/// Used for efficient network synchronization.
/// </summary>
public struct DeltaState
{
    public long ObjectId { get; }
    public VariableChange[]? Changes;
    public int Count;
    private bool _pooled;

    public DeltaState(long objectId, VariableChange[]? changes, int count, bool pooled = false)
    {
        ObjectId = objectId;
        Changes = changes;
        Count = count;
        _pooled = pooled;
    }

    public bool HasChanges => Count > 0;

    public void ReturnToPool()
    {
        if (_pooled && Changes != null)
        {
            System.Buffers.ArrayPool<VariableChange>.Shared.Return(Changes);
            Changes = null;
            _pooled = false;
        }
    }
}
