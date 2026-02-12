using System.Threading.Tasks;

namespace Shared
{
    public interface INetworkPeer
    {
        ValueTask SendAsync(string data);
    }
}
