using System.Threading.Tasks;

namespace Shared.Interfaces;

public interface IMessageHandler<T> where T : INetworkMessage
{
    Task HandleAsync(INetworkPeer peer, T message);
}
