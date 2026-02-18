using System.Threading.Tasks;

namespace Shared
{
    public interface INetworkPeer
    {
        ValueTask SendAsync(string data);
        ValueTask SendAsync(byte[] data);
        ValueTask SendAsync(byte[] data, int offset, int length);
        System.Collections.Generic.IDictionary<int, long> LastSentVersions { get; }

        /// <summary>
        /// Removes entries from the tracking dictionary if they are no longer present in the world.
        /// </summary>
        void PruneLastSentVersions(System.Collections.Generic.IEnumerable<int> activeIds);
    }
}
