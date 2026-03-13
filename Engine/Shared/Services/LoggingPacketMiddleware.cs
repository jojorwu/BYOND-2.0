using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Shared.Interfaces;
using Shared.Models;

namespace Shared.Services;

public class LoggingPacketMiddleware : IPacketMiddleware
{
    private readonly ILogger<LoggingPacketMiddleware> _logger;

    public LoggingPacketMiddleware(ILogger<LoggingPacketMiddleware> logger)
    {
        _logger = logger;
    }

    public ValueTask<bool> ProcessAsync(PacketContext context)
    {
        _logger.LogDebug("Incoming packet: Type={TypeId}, PayloadLength={PayloadLength}, Peer={Peer}",
            context.TypeId, context.Payload.Length, context.Peer.Nickname ?? context.Peer.EndPoint?.ToString());
        return new ValueTask<bool>(true);
    }
}
