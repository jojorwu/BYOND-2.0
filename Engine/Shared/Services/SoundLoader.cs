using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using Shared.Config;
using Shared.Interfaces;

namespace Shared.Services;

public class SoundDefinition
{
    public string Path { get; }
    public SoundDefinition(string path) => Path = path;
}

public interface ISoundRegistry
{
    bool TryGetSound(string path, out SoundDefinition? definition);
}

public class SoundLoader : IResourceLoader<SoundDefinition>
{
    private readonly ISoundRegistry _soundRegistry;

    public SoundLoader(ISoundRegistry soundRegistry)
    {
        _soundRegistry = soundRegistry;
    }

    public Task<SoundDefinition?> LoadAsync(Stream stream, string path)
    {
        // In a real engine, we'd read from the stream
        if (_soundRegistry.TryGetSound(path, out var definition))
        {
            return Task.FromResult(definition);
        }

        return Task.FromResult<SoundDefinition?>(new SoundDefinition(path));
    }
}
