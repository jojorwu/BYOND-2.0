using Shared.Models;
using Shared.Interfaces;
using Shared.Enums;
using Shared.Operations;
using Shared.Services;
namespace Core
{
    public enum SnapshotMessageType : byte
    {
        Full,
        Delta
    }

    public enum DeltaActionType : byte
    {
        New,
        Move,
        Delete
    }
}
