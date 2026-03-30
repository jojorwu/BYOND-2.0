using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Shared.Interfaces;
using Shared.Utils;

namespace Shared.Services;

public class VfsManager : EngineService, IVfsManager
{
    private readonly List<IVfsSource> _sources = new();
    private readonly ConcurrentDictionary<string, IVfsSource?> _cache = new();
    private readonly ReaderWriterLockSlim _lock = new();

    public VfsManager()
    {
    }

    public void Mount(IVfsSource source)
    {
        _lock.EnterWriteLock();
        try
        {
            _sources.Add(source);
            _sources.Sort((a, b) => b.Priority.CompareTo(a.Priority));
            _cache.Clear();
            source.FileChanged += OnSourceFileChanged;
        }
        finally
        {
            _lock.ExitWriteLock();
        }
    }

    public void Unmount(string sourceName)
    {
        _lock.EnterWriteLock();
        try
        {
            var source = _sources.FirstOrDefault(s => s.Name == sourceName);
            if (source != null)
            {
                _sources.Remove(source);
                source.FileChanged -= OnSourceFileChanged;
                source.Dispose();
                _cache.Clear();
            }
        }
        finally
        {
            _lock.ExitWriteLock();
        }
    }

    private void OnSourceFileChanged(string path)
    {
        // For efficiency, only clear the path from cache, not the whole cache
        _cache.TryRemove(path, out _);
        FileChanged?.Invoke(path);
    }

    public bool Exists(string path)
    {
        var source = FindSource(path);
        return source != null;
    }

    public async Task<VfsEntry?> GetEntryAsync(string path)
    {
        var source = FindSource(path);
        return source != null ? await source.GetEntryAsync(path) : null;
    }

    public async Task<Stream?> OpenReadAsync(string path)
    {
        var source = FindSource(path);
        return source != null ? await source.OpenReadAsync(path) : null;
    }

    public async Task<IReadOnlyList<VfsEntry>> ListAsync(string path)
    {
        _lock.EnterReadLock();
        try
        {
            var entries = new Dictionary<string, VfsEntry>();
            foreach (var source in _sources)
            {
                var sourceEntries = await source.ListAsync(path);
                foreach (var entry in sourceEntries)
                {
                    if (!entries.ContainsKey(entry.Path))
                        entries[entry.Path] = entry;
                }
            }
            return entries.Values.ToList();
        }
        finally
        {
            _lock.ExitReadLock();
        }
    }

    private IVfsSource? FindSource(string path)
    {
        if (_cache.TryGetValue(path, out var source))
            return source;

        _lock.EnterReadLock();
        try
        {
            foreach (var s in _sources)
            {
                if (s.Exists(path))
                {
                    _cache.TryAdd(path, s);
                    return s;
                }
            }
        }
        finally
        {
            _lock.ExitReadLock();
        }

        _cache.TryAdd(path, null);
        return null;
    }

    protected override Task OnStopAsync(CancellationToken cancellationToken)
    {
        _lock.EnterWriteLock();
        try
        {
            foreach (var source in _sources)
            {
                source.FileChanged -= OnSourceFileChanged;
                source.Dispose();
            }
            _sources.Clear();
            _cache.Clear();
        }
        finally
        {
            _lock.ExitWriteLock();
        }
        return base.OnStopAsync(cancellationToken);
    }

    public event Action<string>? FileChanged;
}
