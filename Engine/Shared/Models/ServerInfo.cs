using Shared.Models;
using Shared.Interfaces;
using Shared.Enums;
using Shared.Operations;
using Shared.Services;
namespace Shared.Models
{
    public class ServerInfo
    {
        public required string ServerName { get; set; }
        public required string ServerDescription { get; set; }
        public int MaxPlayers { get; set; }
        public required string AssetUrl { get; set; }
    }
}
