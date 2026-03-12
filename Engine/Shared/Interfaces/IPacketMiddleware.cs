using System.Threading.Tasks;
using Shared.Models;

namespace Shared.Interfaces;

/// <summary>
/// A middleware for processing incoming network packets.
/// </summary>
public interface IPacketMiddleware
{
    /// <summary>
    /// Processes the packet context. Returns true to continue the pipeline, false to abort.
    /// </summary>
    ValueTask<bool> ProcessAsync(PacketContext context);
}
