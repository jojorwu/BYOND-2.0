using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Threading.Tasks;
using Shared.Interfaces;

namespace Shared.Services;

public class ResourceSystem : IResourceSystem, IShrinkable
{
    private readonly ConcurrentDictionary<string, Task<object?>> _cache = new();
    private readonly List<IResourceProvider> _providers = new();

    public async Task<T?> LoadResourceAsync<T>(string path) where T : class
    {
        var task = _cache.GetOrAdd(path, p => LoadInternalAsync(p));
        var result = await task;
        return result as T;
    }

    private async Task<object?> LoadInternalAsync(string path)
    {
        foreach (var provider in _providers)
        {
            if (provider.CanHandle(path))
            {
                var resource = await provider.LoadAsync(path);
                if (resource != null)
                {
                    return resource;
                }
            }
        }

        return null;
    }

    public void RegisterProvider(IResourceProvider provider)
    {
        _providers.Add(provider);
    }

    public void ClearCache()
    {
        _cache.Clear();
    }

    public void Shrink() => ClearCache();
}
