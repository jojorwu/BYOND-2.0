using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Threading.Tasks;
using Shared.Interfaces;

namespace Shared.Services;

public class ResourceSystem : IResourceSystem
{
    private readonly ConcurrentDictionary<string, object> _cache = new();
    private readonly List<IResourceProvider> _providers = new();

    public async Task<T?> LoadResourceAsync<T>(string path) where T : class
    {
        if (_cache.TryGetValue(path, out var cached))
        {
            return cached as T;
        }

        foreach (var provider in _providers)
        {
            if (provider.CanHandle(path))
            {
                var resource = await provider.LoadAsync(path);
                if (resource != null)
                {
                    _cache[path] = resource;
                    return resource as T;
                }
            }
        }

        return null;
    }

    public void RegisterProvider(IResourceProvider provider)
    {
        _providers.Add(provider);
    }
}
