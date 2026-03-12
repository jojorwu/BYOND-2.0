using System.Collections.Generic;

namespace Shared.Models;

/// <summary>
/// Represents a set of changes to a game object's state.
/// Used for efficient network synchronization.
/// </summary>
public struct DeltaState
{
    public long ObjectId { get; }
    public Dictionary<int, DreamValue> ChangedVariables { get; }

    public DeltaState(long objectId)
    {
        ObjectId = objectId;
        ChangedVariables = new Dictionary<int, DreamValue>();
    }

    public void AddChange(int index, DreamValue value)
    {
        ChangedVariables[index] = value;
    }

    public bool HasChanges => ChangedVariables.Count > 0;
}
