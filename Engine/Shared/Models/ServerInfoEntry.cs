namespace Shared;
    /// <summary>
    /// Represents a single server entry in the server browser list.
    /// </summary>
    public class ServerInfoEntry
    {
        public required string Name { get; set; }
        public required string Address { get; set; }
        public int CurrentPlayers { get; set; }
        public int MaxPlayers { get; set; }
        public int Ping { get; set; } = -1; // Default to -1 (not yet pinged)
        public bool IsFavorite { get; set; } = false;
    }
