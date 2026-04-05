using Shared.Utils;

namespace Shared.Interfaces;

/// <summary>
/// A zero-allocation, struct-based component.
/// Stored in contiguous memory slabs within archetypes.
/// </summary>
public interface IDataComponent
{
    // No methods to keep it as data-only for SIMD friendliness
}
