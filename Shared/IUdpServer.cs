namespace Shared
{
    public interface IUdpServer
    {
        void BroadcastSnapshot(string snapshot);
        void BroadcastSnapshot(Region region, string snapshot);
    }
}
