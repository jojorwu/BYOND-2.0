using System.Collections.Generic;
using System.Threading.Tasks;
using Shared;

namespace Core.Networking
{
    public class MockServerDiscoveryService : IServerDiscoveryService
    {
        public Task<IEnumerable<ServerInfoEntry>> GetServerListAsync()
        {
            var servers = new List<ServerInfoEntry>
            {
                new ServerInfoEntry { Name = "Llama Land", Address = "127.0.0.1:7777", CurrentPlayers = 15, MaxPlayers = 50 },
                new ServerInfoEntry { Name = "Goonstation", Address = "8.8.8.8:1234", CurrentPlayers = 45, MaxPlayers = 100 },
                new ServerInfoEntry { Name = "Paradise Station", Address = "192.168.1.100:27015", CurrentPlayers = 80, MaxPlayers = 80 },
                new ServerInfoEntry { Name = "Empty Server", Address = "localhost:1111", CurrentPlayers = 0, MaxPlayers = 20 },
                new ServerInfoEntry { Name = "Super Secret Club", Address = "10.0.0.1:5555", CurrentPlayers = 5, MaxPlayers = 10, IsFavorite = true }
            };

            return Task.FromResult<IEnumerable<ServerInfoEntry>>(servers);
        }
    }
}
