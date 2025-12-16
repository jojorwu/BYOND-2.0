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
