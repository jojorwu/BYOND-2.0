namespace Shared;
    public interface IUdpServer
    {
        void BroadcastSnapshot(string snapshot);
        void BroadcastSnapshot(MergedRegion region, string snapshot);
        void BroadcastSnapshot(MergedRegion region, byte[] snapshot);
        System.Threading.Tasks.Task SendRegionSnapshotAsync(MergedRegion region, System.Collections.Generic.IEnumerable<IGameObject> objects);
    }
