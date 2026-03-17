using System.Threading.Tasks;

namespace Shared.Interfaces;

/// <summary>
/// A centralized system for managing game assets and resources.
/// Supports caching, lazy loading, and provider-based asset resolution.
/// </summary>
public interface IResourceSystem
{
    Task<T?> LoadResourceAsync<T>(string path) where T : class;
    void RegisterProvider(IResourceProvider provider);
    void ClearCache();
}

public interface IResourceProvider
{
    bool CanHandle(string path);
    Task<object?> LoadAsync(string path);
}
