namespace Core
{
    public enum SnapshotMessageType : byte
    {
        Full,
        Delta,
        Binary,
        Json,
        Sound,
        StopSound,
        SyncCVars
    }

    public enum DeltaActionType : byte
    {
        New,
        Move,
        Delete
    }
}
