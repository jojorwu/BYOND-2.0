using System;
using System.IO;
using System.Threading.Tasks;

namespace Shared.Interfaces;

public interface IResourceLoader<T> where T : class
{
    Task<T?> LoadAsync(Stream stream, string path);
}

public interface IResourceSystem : IEngineService
{
    Task<T?> LoadResourceAsync<T>(string path) where T : class;
    void RegisterLoader<T>(IResourceLoader<T> loader) where T : class;
    void ClearCache();

    event Action<string>? ResourceReloaded;
}
