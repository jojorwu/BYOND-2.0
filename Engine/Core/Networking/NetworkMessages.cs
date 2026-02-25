namespace Core
{
    public enum SnapshotMessageType : byte
    {
        Full,
        Delta,
        Binary,
        Json
    }

    public enum DeltaActionType : byte
    {
        New,
        Move,
        Delete
    }
}
