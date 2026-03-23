namespace Shared.Interfaces;

/// <summary>
/// Defines a service that can be "frozen" into a high-performance, immutable state.
/// This is typically used for registries and managers after initial population to
/// leverage .NET 8+ FrozenDictionary and FrozenSet for optimized lookups.
/// </summary>
public interface IFreezable
{
    /// <summary>
    /// Freezes the service, making it read-only and optimizing internal structures.
    /// </summary>
    void Freeze();
}
