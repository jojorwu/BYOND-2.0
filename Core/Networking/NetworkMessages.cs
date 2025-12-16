namespace Core.Networking
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
