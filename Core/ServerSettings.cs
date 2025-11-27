namespace Core
{
    public class ServerSettings
    {
        public string IpAddress { get; set; } = "127.0.0.1";
        public int Port { get; set; } = 7777;
        public ThreadMode ThreadManagement { get; set; } = ThreadMode.Automatic;
    }

    public enum ThreadMode
    {
        Automatic,
        Manual
    }
}
