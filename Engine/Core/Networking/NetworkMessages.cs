namespace Core
{
    public enum SnapshotMessageType : byte
    {
        Full,
        Delta,
        Binary,
        Json,
        Sound,
        StopSound
    }

    public enum DeltaActionType : byte
    {
        New,
        Move,
        Delete
    }
}
