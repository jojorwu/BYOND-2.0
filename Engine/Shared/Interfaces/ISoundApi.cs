using Shared;

namespace Shared;

public interface ISoundApi
{
    void Play(string file, float volume = 100f, float pitch = 1f, bool repeat = false);
    void PlayAt(string file, long x, long y, long z, float volume = 100f, float pitch = 1f, float falloff = 1f);
    void PlayOn(string file, IGameObject obj, float volume = 100f, float pitch = 1f, float falloff = 1f);
}
