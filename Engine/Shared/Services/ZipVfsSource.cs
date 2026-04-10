using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Threading.Tasks;
using Shared.Interfaces;
using Shared.Utils;

namespace Shared.Services;

public class ZipVfsSource : IVfsSource
{
    private readonly string _zipPath;
    private readonly ZipArchive _archive;
    private readonly Dictionary<string, ZipArchiveEntry> _entries;

    public string Name { get; }
    public int Priority { get; }

    public ZipVfsSource(string name, string zipPath, int priority = 0)
    {
        Name = name;
        _zipPath = Path.GetFullPath(zipPath);
        Priority = priority;
        _archive = ZipFile.OpenRead(_zipPath);
        _entries = _archive.Entries.ToDictionary(e => e.FullName.Replace('\\', '/').Trim('/'), e => e, StringComparer.OrdinalIgnoreCase);
    }

    private string NormalizePath(string path)
    {
        return path.Replace('\\', '/').Trim('/');
    }

    public bool Exists(string path)
    {
        var normalized = NormalizePath(path);
        return _entries.ContainsKey(normalized);
    }

    public Task<VfsEntry?> GetEntryAsync(string path)
    {
        var normalized = NormalizePath(path);
        if (_entries.TryGetValue(normalized, out var entry))
        {
            return Task.FromResult<VfsEntry?>(new VfsEntry(path, false, entry.Length, entry.LastWriteTime.DateTime));
        }
        // Directories in zips are implicit or explicitly stored with a trailing slash
        if (_entries.Keys.Any(k => k.StartsWith(normalized + "/", StringComparison.OrdinalIgnoreCase)))
        {
            return Task.FromResult<VfsEntry?>(new VfsEntry(path, true, 0, DateTime.MinValue));
        }
        return Task.FromResult<VfsEntry?>(null);
    }

    public Task<Stream?> OpenReadAsync(string path)
    {
        var normalized = NormalizePath(path);
        if (_entries.TryGetValue(normalized, out var entry))
        {
            // ZipArchiveEntry.Open() returns a non-seekable stream.
            // For a better experience, we can buffer it if necessary, but for now, return as is.
            return Task.FromResult<Stream?>(entry.Open());
        }
        return Task.FromResult<Stream?>(null);
    }

    public Task<IReadOnlyList<VfsEntry>> ListAsync(string path)
    {
        var normalized = NormalizePath(path);
        var entries = new List<VfsEntry>();
        var prefix = normalized.Length > 0 ? normalized + "/" : "";

        var added = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var key in _entries.Keys)
        {
            if (key.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
            {
                var relative = key.Substring(prefix.Length);
                var parts = relative.Split('/');
                var first = parts[0];
                if (added.Add(first))
                {
                    bool isDir = parts.Length > 1;
                    if (isDir)
                    {
                        entries.Add(new VfsEntry(prefix + first, true, 0, DateTime.MinValue));
                    }
                    else
                    {
                        var entry = _entries[key];
                        entries.Add(new VfsEntry(key, false, entry.Length, entry.LastWriteTime.DateTime));
                    }
                }
            }
        }
        return Task.FromResult<IReadOnlyList<VfsEntry>>(entries);
    }

    public void Dispose()
    {
        _archive.Dispose();
    }

    public event Action<string>? FileChanged { add { } remove { } }
}
