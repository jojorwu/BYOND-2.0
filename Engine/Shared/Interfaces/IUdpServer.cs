namespace Shared
{
    public interface IUdpServer
    {
        void BroadcastSnapshot(string snapshot);
        void BroadcastSnapshot(MergedRegion region, string snapshot);
        void BroadcastSnapshot(MergedRegion region, byte[] snapshot);
        void SendRegionSnapshot(MergedRegion region, System.Collections.Generic.IEnumerable<IGameObject> objects);
    }
}
