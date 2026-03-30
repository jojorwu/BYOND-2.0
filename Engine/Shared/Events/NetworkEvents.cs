using Shared.Models;

namespace Shared.Events;

public class SoundEvent
{
    public SoundData Data { get; }
    public SoundEvent(SoundData data) => Data = data;
}

public class StopSoundEvent
{
    public string File { get; }
    public long? ObjectId { get; }
    public StopSoundEvent(string file, long? objectId)
    {
        File = file;
        ObjectId = objectId;
    }
}

public class CVarSyncEvent
{
    public string Key { get; }
    public object Value { get; }
    public CVarSyncEvent(string key, object value)
    {
        Key = key;
        Value = value;
    }
}
