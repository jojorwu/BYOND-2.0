using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Shared.Interfaces;
using Shared.Utils;

namespace Shared.Services;

public class LocalVfsSource : IVfsSource
{
    private readonly string _rootPath;
    private readonly FileSystemWatcher? _watcher;

    public string Name { get; }
    public int Priority { get; }

    public LocalVfsSource(string name, string rootPath, int priority = 0, bool watchForChanges = false)
    {
        Name = name;
        _rootPath = Path.GetFullPath(rootPath);
        Priority = priority;

        if (watchForChanges && Directory.Exists(_rootPath))
        {
            _watcher = new FileSystemWatcher(_rootPath)
            {
                IncludeSubdirectories = true,
                NotifyFilter = NotifyFilters.LastWrite | NotifyFilters.FileName | NotifyFilters.DirectoryName
            };
            _watcher.Changed += (s, e) => FileChanged?.Invoke(GetRelativePath(e.FullPath));
            _watcher.Created += (s, e) => FileChanged?.Invoke(GetRelativePath(e.FullPath));
            _watcher.Deleted += (s, e) => FileChanged?.Invoke(GetRelativePath(e.FullPath));
            _watcher.Renamed += (s, e) => {
                FileChanged?.Invoke(GetRelativePath(e.OldFullPath));
                FileChanged?.Invoke(GetRelativePath(e.FullPath));
            };
            _watcher.EnableRaisingEvents = true;
        }
    }

    private string GetRelativePath(string fullPath)
    {
        return Path.GetRelativePath(_rootPath, fullPath).Replace('\\', '/');
    }

    private string GetFullPath(string relativePath)
    {
        return Path.Combine(_rootPath, relativePath.Replace('/', Path.DirectorySeparatorChar));
    }

    public bool Exists(string path)
    {
        var fullPath = GetFullPath(path);
        return File.Exists(fullPath) || Directory.Exists(fullPath);
    }

    public Task<VfsEntry?> GetEntryAsync(string path)
    {
        var fullPath = GetFullPath(path);
        if (File.Exists(fullPath))
        {
            var info = new FileInfo(fullPath);
            return Task.FromResult<VfsEntry?>(new VfsEntry(path, false, info.Length, info.LastWriteTime));
        }
        if (Directory.Exists(fullPath))
        {
            var info = new DirectoryInfo(fullPath);
            return Task.FromResult<VfsEntry?>(new VfsEntry(path, true, 0, info.LastWriteTime));
        }
        return Task.FromResult<VfsEntry?>(null);
    }

    public Task<Stream?> OpenReadAsync(string path)
    {
        var fullPath = GetFullPath(path);
        if (File.Exists(fullPath))
        {
            return Task.FromResult<Stream?>(new FileStream(fullPath, FileMode.Open, FileAccess.Read, FileShare.Read));
        }
        return Task.FromResult<Stream?>(null);
    }

    public Task<IReadOnlyList<VfsEntry>> ListAsync(string path)
    {
        var fullPath = GetFullPath(path);
        if (Directory.Exists(fullPath))
        {
            var entries = new List<VfsEntry>();
            foreach (var file in Directory.GetFiles(fullPath))
            {
                var info = new FileInfo(file);
                entries.Add(new VfsEntry(GetRelativePath(file), false, info.Length, info.LastWriteTime));
            }
            foreach (var dir in Directory.GetDirectories(fullPath))
            {
                var info = new DirectoryInfo(dir);
                entries.Add(new VfsEntry(GetRelativePath(dir), true, 0, info.LastWriteTime));
            }
            return Task.FromResult<IReadOnlyList<VfsEntry>>(entries);
        }
        return Task.FromResult<IReadOnlyList<VfsEntry>>(Array.Empty<VfsEntry>());
    }

    public void Dispose()
    {
        _watcher?.Dispose();
    }

    public event Action<string>? FileChanged;
}
