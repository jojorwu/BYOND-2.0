namespace Shared.Interfaces;

/// <summary>
/// Defines a service that can be "frozen" to optimize its internal data structures
/// for read-only access after the initial configuration phase.
/// </summary>
public interface IFreezable
{
    /// <summary>
    /// Transition internal data structures to highly optimized, immutable representations (e.g., FrozenDictionary).
    /// </summary>
    void Freeze();
}
