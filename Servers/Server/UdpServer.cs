using Shared;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Shared.Services;

namespace Server
{
    public class UdpServer : EngineService, IHostedService, IUdpServer
    {
        public override int Priority => 40; // High priority
        private readonly INetworkService _networkService;
        private readonly NetworkEventHandler _networkEventHandler;
        private readonly IServerContext _context;

        public UdpServer(INetworkService networkService, NetworkEventHandler networkEventHandler, IServerContext context)
        {
            _networkService = networkService;
            _networkEventHandler = networkEventHandler;
            _context = context;
        }

        public override Task StartAsync(CancellationToken cancellationToken)
        {
            _networkEventHandler.SubscribeToEvents();
            _networkService.Start();
            return Task.CompletedTask;
        }

        public override Task StopAsync(CancellationToken cancellationToken)
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
                _context.PlayerManager.ForEachPlayerInRegion(r, peer => peer.Send(snapshot));
        }
    }
}
