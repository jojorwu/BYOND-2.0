using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using Shared.Interfaces;
using Shared.Attributes;

namespace Shared.Services;

[EngineService(typeof(IResourceSystem))]
public class ResourceSystem : EngineService, IResourceSystem, IShrinkable
{
    private readonly IVfsManager _vfs;
    private readonly ConcurrentDictionary<string, Task<object?>> _cache = new();
    private readonly ConcurrentDictionary<Type, object> _loaders = new();
    private readonly IDiagnosticBus _diagnosticBus;

    private long _totalRequests;
    private long _cacheHits;
    private long _cacheMisses;

    public ResourceSystem(IVfsManager vfs, IDiagnosticBus diagnosticBus)
    {
        _vfs = vfs;
        _diagnosticBus = diagnosticBus;
        _vfs.FileChanged += OnVfsFileChanged;
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
        var task = _cache.GetOrAdd(path, p => LoadInternalAsync<T>(p));
        var result = await task;
        return result as T;
    }

    private async Task<object?> LoadInternalAsync<T>(string path) where T : class
    {
        if (!_loaders.TryGetValue(typeof(T), out var loaderObj) || loaderObj is not IResourceLoader<T> loader)
        {
            _diagnosticBus.Publish("ResourceSystem", $"No loader registered for type {typeof(T).Name}", DiagnosticSeverity.Warning);
            return null;
        }

        using var stream = await _vfs.OpenReadAsync(path);
        if (stream == null)
        {
            _diagnosticBus.Publish("ResourceSystem", $"Resource not found: {path}", DiagnosticSeverity.Warning);
            return null;
        }

        var resource = await loader.LoadAsync(stream, path);
        if (resource != null)
        {
            _diagnosticBus.Publish("ResourceSystem", "Resource loaded", DiagnosticSeverity.Info, m => {
                m.Add("Path", path);
                m.Add("Type", typeof(T).Name);
            });
            return resource;
        }

        return null;
    }

    public void RegisterLoader<T>(IResourceLoader<T> loader) where T : class
    {
        _loaders[typeof(T)] = loader;
    }

    private void OnVfsFileChanged(string path)
    {
        if (_cache.TryRemove(path, out _))
        {
            _diagnosticBus.Publish("ResourceSystem", "Resource invalidated", DiagnosticSeverity.Info, m => m.Add("Path", path));
            ResourceReloaded?.Invoke(path);
        }
    }

    protected override Task OnStopAsync(CancellationToken cancellationToken)
    {
        _vfs.FileChanged -= OnVfsFileChanged;
        ClearCache();
        return base.OnStopAsync(cancellationToken);
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
        info["LoaderCount"] = _loaders.Count;
        return info;
    }

    public event Action<string>? ResourceReloaded;
}
