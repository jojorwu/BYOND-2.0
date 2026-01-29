using System;
using System.Text.Json;
using LiteNetLib;
using Shared;

namespace Server
{
    public class NetworkEventHandler
    {
        private readonly INetworkService _networkService;
        private readonly IServerContext _context;
        private readonly IScriptHost _scriptHost;

        public NetworkEventHandler(INetworkService networkService, IServerContext context, IScriptHost scriptHost)
        {
            _networkService = networkService;
            _context = context;
            _scriptHost = scriptHost;
        }

        public void SubscribeToEvents()
        {
            _networkService.PeerConnected += OnPeerConnected;
            _networkService.PeerDisconnected += OnPeerDisconnected;
            _networkService.CommandReceived += OnCommandReceived;
        }

        private void OnPeerConnected(INetworkPeer peer)
        {
            _context.PlayerManager.AddPlayer(peer);

            var serverInfo = new ServerInfo
            {
                ServerName = _context.Settings.ServerName,
                ServerDescription = _context.Settings.ServerDescription,
                MaxPlayers = _context.Settings.MaxPlayers,
                AssetUrl = $"http://{_context.Settings.Network.IpAddress}:{_context.Settings.HttpServer.Port}"
            };

            var json = JsonSerializer.Serialize(serverInfo);
            peer.Send(json);
        }

        private void OnPeerDisconnected(INetworkPeer peer, DisconnectInfo disconnectInfo)
        {
            _context.PlayerManager.RemovePlayer(peer);
        }

        private void OnCommandReceived(INetworkPeer peer, string command)
        {
            _scriptHost.EnqueueCommand(command, (result) => {
                peer.Send(result);
            });
        }

        public void UnsubscribeFromEvents()
        {
            _networkService.PeerConnected -= OnPeerConnected;
            _networkService.PeerDisconnected -= OnPeerDisconnected;
            _networkService.CommandReceived -= OnCommandReceived;
        }
    }
}
