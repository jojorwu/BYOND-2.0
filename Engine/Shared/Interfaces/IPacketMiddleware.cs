using System.Threading.Tasks;
using Shared.Interfaces;
using Shared.Models;

namespace Shared.Interfaces;
    /// <summary>
    /// Middleware for processing network packets before they reach their handlers.
    /// </summary>
    public interface IPacketMiddleware
    {
        /// <summary>
        /// Processes a packet context.
        /// </summary>
        /// <param name="context">The context of the packet being processed.</param>
        /// <returns>True if the processing should continue; otherwise, false.</returns>
        Task<bool> ProcessAsync(PacketContext context);
    }
