using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Shared.Interfaces;

namespace Shared.Services;

public class ResourceSystem : EngineService, IResourceSystem, IShrinkable
{
    private readonly ConcurrentDictionary<string, Task<object?>> _cache = new();
    private readonly List<IResourceProvider> _providers = new();
    private readonly IDiagnosticBus _diagnosticBus;
    private long _totalRequests;
    private long _cacheHits;
    private long _cacheMisses;

    public ResourceSystem(IDiagnosticBus diagnosticBus)
    {
        _diagnosticBus = diagnosticBus;
    }

    public async Task<T?> LoadResourceAsync<T>(string path) where T : class
    {
        Interlocked.Increment(ref _totalRequests);

        if (_cache.TryGetValue(path, out var cachedTask))
        {
            Interlocked.Increment(ref _cacheHits);
            var cachedResult = await cachedTask;
            return cachedResult as T;
        }

        Interlocked.Increment(ref _cacheMisses);
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
                    _diagnosticBus.Publish("ResourceSystem", "Resource loaded", DiagnosticSeverity.Info, m => {
                        m.Add("Path", path);
                        m.Add("Type", resource.GetType().Name);
                    });
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

    protected override Task OnStopAsync(CancellationToken cancellationToken)
    {
        ClearCache();
        return Task.CompletedTask;
    }

    public void ClearCache()
    {
        _cache.Clear();
    }

    public void Shrink() => ClearCache();

    public override Dictionary<string, object> GetDiagnosticInfo()
    {
        var info = base.GetDiagnosticInfo();
        info["CacheSize"] = _cache.Count;
        info["TotalRequests"] = Interlocked.Read(ref _totalRequests);
        info["CacheHits"] = Interlocked.Read(ref _cacheHits);
        info["CacheMisses"] = Interlocked.Read(ref _cacheMisses);
        info["ProviderCount"] = _providers.Count;
        return info;
    }
}
