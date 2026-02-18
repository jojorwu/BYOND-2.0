using Shared.Attributes;
using Shared.Interfaces;
using Shared.Models;
using Shared;
using Microsoft.Extensions.Options;
using System.Threading.Tasks;

namespace Server.Systems
{
    [System("NetworkingSystem", Priority = -10)] // Run after script system
    [Resource(typeof(IUdpServer), ResourceAccess.Write)]
    [Resource(typeof(IGameStateSnapshotter), ResourceAccess.Read)]
    public class NetworkingSystem : BaseSystem
    {
        private readonly IUdpServer _udpServer;
        private readonly IGameStateSnapshotter _snapshotter;
        private readonly IGameState _gameState;
        private readonly ServerSettings _settings;

        public NetworkingSystem(IUdpServer udpServer, IGameStateSnapshotter snapshotter, IGameState gameState, IOptions<ServerSettings> settings)
        {
            _udpServer = udpServer;
            _snapshotter = snapshotter;
            _gameState = gameState;
            _settings = settings.Value;
        }

        public override bool Enabled => !_settings.Performance.EnableRegionalProcessing;

        public override void Tick(IEntityCommandBuffer ecb)
        {
            var snapshot = _snapshotter.GetSnapshot(_gameState);
            // Non-blocking broadcast
            Task.Run(() => _udpServer.BroadcastSnapshot(snapshot));
        }
    }
}
