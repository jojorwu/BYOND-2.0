using System.Threading.Tasks;

namespace Shared
{
    public interface INetworkPeer
    {
        ValueTask SendAsync(string data);
        ValueTask SendAsync(byte[] data);
        System.Collections.Generic.IDictionary<int, long> LastSentVersions { get; }
    }
}
