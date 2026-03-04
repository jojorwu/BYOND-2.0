using System.Threading.Tasks;

namespace Shared;
    public interface INetworkPeer
    {
        ValueTask SendAsync(string data);
        ValueTask SendAsync(byte[] data);
        ValueTask SendAsync(System.ReadOnlyMemory<byte> data);
        System.Collections.Generic.IDictionary<long, long> LastSentVersions { get; }
    }
