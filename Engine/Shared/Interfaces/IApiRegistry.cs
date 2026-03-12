using System.Collections.Generic;

namespace Shared.Interfaces;

/// <summary>
/// A provider for a specific sub-API (e.g., Map, Objects, Sounds).
/// </summary>
public interface IApiProvider
{
    /// <summary>
    /// The name of the API segment.
    /// </summary>
    string Name { get; }
}

/// <summary>
/// Registry for managing and resolving game API providers.
/// </summary>
public interface IApiRegistry
{
    void Register<T>(T provider) where T : class, IApiProvider;
    T Get<T>(string name) where T : class, IApiProvider;
    IEnumerable<IApiProvider> GetAll();
}
