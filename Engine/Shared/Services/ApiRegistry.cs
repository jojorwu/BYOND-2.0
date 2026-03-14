using System.Collections.Concurrent;
using System.Collections.Generic;
using Microsoft.Extensions.DependencyInjection;
using Shared.Interfaces;

namespace Shared.Services;

public class ApiRegistry : IApiRegistry
{
    private readonly ConcurrentDictionary<string, IApiProvider> _providers = new();
    private readonly ConcurrentDictionary<Type, IApiProvider> _typeProviders = new();
    private volatile IApiProvider[] _allProviders = Array.Empty<IApiProvider>();

    public void Register<T>(T provider) where T : class, IApiProvider
    {
        _providers[provider.Name.ToLowerInvariant()] = provider;

        var type = typeof(T);
        if (type.IsInterface) _typeProviders[type] = provider;
        _typeProviders[provider.GetType()] = provider;

        lock (_providers)
        {
            _allProviders = _providers.Values.ToArray();
        }
    }

    public void RegisterAll(System.IServiceProvider serviceProvider)
    {
        var providers = serviceProvider.GetServices<IApiProvider>();
        foreach (var provider in providers)
        {
            Register(provider);
        }
    }

    public T Get<T>(string? name = null) where T : class, IApiProvider
    {
        if (name != null)
        {
            if (_providers.TryGetValue(name.ToLowerInvariant(), out var provider))
            {
                return (T)provider;
            }
        }
        else if (_typeProviders.TryGetValue(typeof(T), out var provider))
        {
            return (T)provider;
        }

        throw new KeyNotFoundException($"API provider '{name ?? typeof(T).Name}' not found.");
    }

    public IEnumerable<IApiProvider> GetAll() => _allProviders;
}
