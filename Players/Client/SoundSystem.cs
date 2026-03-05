using Shared;
using System.Numerics;

namespace Client;

public interface ISoundSystem
{
    void Play(SoundData sound);
    void Update(Vector3 listenerPosition, Vector3 listenerForward);
}

public class MockSoundSystem : ISoundSystem
{
    public void Play(SoundData sound)
    {
        Console.WriteLine($"[DEBUG] Playing sound: {sound.File} (Vol: {sound.Volume}, Pitch: {sound.Pitch}, Repeat: {sound.Repeat})");
        if (sound.X.HasValue) Console.WriteLine($"        at ({sound.X}, {sound.Y}, {sound.Z})");
        if (sound.ObjectId.HasValue) Console.WriteLine($"        attached to object: {sound.ObjectId}");
    }

    public void Update(Vector3 listenerPosition, Vector3 listenerForward) { }
}
