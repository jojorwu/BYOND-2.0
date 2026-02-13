namespace Core
{
    public enum SnapshotMessageType : byte
    {
        Full,
        Delta,
        Binary
    }

    public enum DeltaActionType : byte
    {
        New,
        Move,
        Delete
    }
}
