using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;

namespace Shared.Interfaces;

public record VfsEntry(string Path, bool IsDirectory, long Size, DateTime ModifiedTime);

public interface IVfsSource : IDisposable
{
    string Name { get; }
    int Priority { get; }

    bool Exists(string path);
    Task<VfsEntry?> GetEntryAsync(string path);
    Task<Stream?> OpenReadAsync(string path);
    Task<IReadOnlyList<VfsEntry>> ListAsync(string path);

    event Action<string>? FileChanged;
}

public interface IVfsManager : IEngineService
{
    void Mount(IVfsSource source);
    void Unmount(string sourceName);

    bool Exists(string path);
    Task<VfsEntry?> GetEntryAsync(string path);
    Task<Stream?> OpenReadAsync(string path);
    Task<IReadOnlyList<VfsEntry>> ListAsync(string path);

    event Action<string>? FileChanged;
}
