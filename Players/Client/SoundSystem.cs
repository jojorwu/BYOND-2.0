using Shared;
using System.Numerics;

namespace Client;

public interface ISoundSystem
{
    void Play(SoundData sound);
    void Update(Vector3 listenerPosition, Vector3 listenerForward);
    void UpdateObjectPosition(long objectId, Vector3 position);
    void Stop(string file, long? objectId = null);
}

public class MockSoundSystem : ISoundSystem
{
    private readonly Dictionary<long, string> _attachedSounds = new();

    public void Play(SoundData sound)
    {
        Console.WriteLine($"[DEBUG] Playing sound: {sound.File} (Vol: {sound.Volume}, Pitch: {sound.Pitch}, Repeat: {sound.Repeat})");
        if (sound.X.HasValue) Console.WriteLine($"        at ({sound.X}, {sound.Y}, {sound.Z})");
        if (sound.ObjectId.HasValue)
        {
            Console.WriteLine($"        attached to object: {sound.ObjectId}");
            _attachedSounds[sound.ObjectId.Value] = sound.File;
        }
    }

    public void Update(Vector3 listenerPosition, Vector3 listenerForward) { }

    public void UpdateObjectPosition(long objectId, Vector3 position)
    {
        if (_attachedSounds.TryGetValue(objectId, out var file))
        {
            // In a real system, we'd update the 3D source position here
            // Console.WriteLine($"[DEBUG] Updating 3D position for sound {file} on object {objectId} to {position}");
        }
    }

    public void Stop(string file, long? objectId = null)
    {
        Console.WriteLine($"[DEBUG] Stopping sound: {file}{(objectId.HasValue ? $" on object {objectId.Value}" : "")}");
        if (objectId.HasValue) _attachedSounds.Remove(objectId.Value);
    }
}
