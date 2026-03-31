using System;
using System.Threading.Tasks;

namespace Shared.Interfaces;

public interface INetworkSender
{
    ValueTask SendAsync(INetworkPeer peer, INetworkMessage message);
}
