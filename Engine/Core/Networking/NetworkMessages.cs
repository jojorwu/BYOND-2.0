namespace Core
{
    public enum SnapshotMessageType : byte
    {
        Full,
        Delta,
        Binary,
        BitPackedDelta,
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
