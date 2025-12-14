using Shared;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace Server
{
    public class UdpServer : IHostedService, IUdpServer
    {
        private readonly INetworkService _networkService;
        private readonly NetworkEventHandler _networkEventHandler;
        private readonly IPlayerManager _playerManager;

        public UdpServer(INetworkService networkService, NetworkEventHandler networkEventHandler, IPlayerManager playerManager)
        {
            _networkService = networkService;
            _networkEventHandler = networkEventHandler;
            _playerManager = playerManager;
        }

        public Task StartAsync(CancellationToken cancellationToken)
        {
            _networkEventHandler.SubscribeToEvents();
            _networkService.Start();
            return Task.CompletedTask;
        }

        public Task StopAsync(CancellationToken cancellationToken)
        {
            _networkEventHandler.UnsubscribeFromEvents();
            _networkService.Stop();
            return Task.CompletedTask;
        }

        public void BroadcastSnapshot(string snapshot) {
            _networkService.BroadcastSnapshot(snapshot);
        }

        public void BroadcastSnapshot(MergedRegion region, string snapshot)
        {
            foreach(var r in region.Regions)
                _playerManager.ForEachPlayerInRegion(r, peer => peer.Send(snapshot));
        }
    }
}
