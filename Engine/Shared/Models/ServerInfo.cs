namespace Shared;
    public class ServerInfo
    {
        public required string ServerName { get; set; }
        public required string ServerDescription { get; set; }
        public int MaxPlayers { get; set; }
        public required string AssetUrl { get; set; }
    }
