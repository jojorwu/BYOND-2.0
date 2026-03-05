namespace Shared;

public struct SoundData
{
    public string File { get; set; }
    public float Volume { get; set; }
    public float Pitch { get; set; }
    public bool Repeat { get; set; }
    public long? X { get; set; }
    public long? Y { get; set; }
    public long? Z { get; set; }
    public long? ObjectId { get; set; }
    public float Falloff { get; set; }

    public SoundData(string file, float volume = 100f, float pitch = 1f, bool repeat = false)
    {
        File = file;
        Volume = volume;
        Pitch = pitch;
        Repeat = repeat;
        X = null;
        Y = null;
        Z = null;
        ObjectId = null;
        Falloff = 1f;
    }
}
