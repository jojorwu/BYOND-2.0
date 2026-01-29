namespace Shared
{
    public interface IUdpServer
    {
        void BroadcastSnapshot(string snapshot);
        void BroadcastSnapshot(MergedRegion region, string snapshot);
    }
}
