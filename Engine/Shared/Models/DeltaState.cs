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

    public DeltaState(long objectId, VariableChange[]? changes, int count)
    {
        ObjectId = objectId;
        Changes = changes;
        Count = count;
    }

    public bool HasChanges => Count > 0;
}
