using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Threading.Tasks;
using Shared.Config;
using Shared.Interfaces;

namespace Shared.Services;

public class SoundResourceProvider : IResourceProvider
{
    private readonly ISoundRegistry _soundRegistry;

    public SoundResourceProvider(ISoundRegistry soundRegistry)
    {
        _soundRegistry = soundRegistry;
    }

    public bool CanHandle(string path)
    {
        return path.EndsWith(".ogg", System.StringComparison.OrdinalIgnoreCase) ||
               path.EndsWith(".wav", System.StringComparison.OrdinalIgnoreCase);
    }

    public Task<object?> LoadAsync(string path)
    {
        // In a real engine, this would load the actual sound data into a buffer.
        // For our architectural demonstration, we check if it's a known sound.
        if (_soundRegistry.TryGetSound(path, out var definition))
        {
            return Task.FromResult<object?>(definition);
        }

        // Mock loading a new sound definition
        return Task.FromResult<object?>(new SoundDefinition(path));
    }
}
