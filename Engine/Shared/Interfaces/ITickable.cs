namespace Shared.Interfaces;

/// <summary>
/// Interface for services that need to perform logic every frame.
/// </summary>
public interface ITickable
{
    /// <summary>
    /// Executes the tick logic asynchronously.
    /// </summary>
    ValueTask TickAsync();
}
