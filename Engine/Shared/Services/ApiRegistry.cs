using System.Collections.Concurrent;
using System.Collections.Generic;
using Shared.Interfaces;

namespace Shared.Services;

public class ApiRegistry : IApiRegistry
{
    private readonly ConcurrentDictionary<string, IApiProvider> _providers = new();

    public void Register<T>(T provider) where T : class, IApiProvider
    {
        _providers[provider.Name.ToLowerInvariant()] = provider;
    }

    public T Get<T>(string name) where T : class, IApiProvider
    {
        if (_providers.TryGetValue(name.ToLowerInvariant(), out var provider))
        {
            return (T)provider;
        }
        throw new KeyNotFoundException($"API provider '{name}' not found.");
    }

    public IEnumerable<IApiProvider> GetAll() => _providers.Values;
}
