namespace Server
{
    public interface IUdpServer
    {
        void BroadcastSnapshot(string snapshot);
    }
}
