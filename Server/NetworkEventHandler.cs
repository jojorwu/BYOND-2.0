using System;
using System.Text.Json;
using LiteNetLib;
using Shared;

namespace Server
{
    public class NetworkEventHandler
    {
        private readonly INetworkService _networkService;
        private readonly IPlayerManager _playerManager;
        private readonly IScriptHost _scriptHost;
        private readonly ServerSettings _settings;

        public NetworkEventHandler(INetworkService networkService, IPlayerManager playerManager, IScriptHost scriptHost, ServerSettings settings)
        {
            _networkService = networkService;
            _playerManager = playerManager;
            _scriptHost = scriptHost;
            _settings = settings;
        }

        public void SubscribeToEvents()
        {
            _networkService.PeerConnected += OnPeerConnected;
            _networkService.PeerDisconnected += OnPeerDisconnected;
            _networkService.CommandReceived += OnCommandReceived;
        }

        private void OnPeerConnected(INetworkPeer peer)
        {
            _playerManager.AddPlayer(peer);

            var serverInfo = new ServerInfo
            {
                ServerName = _settings.ServerName,
                ServerDescription = _settings.ServerDescription,
                MaxPlayers = _settings.MaxPlayers,
                AssetUrl = $"http://{_settings.Network.IpAddress}:{_settings.HttpServer.Port}"
            };

            var json = JsonSerializer.Serialize(serverInfo);
            peer.Send(json);
        }

        private void OnPeerDisconnected(INetworkPeer peer, DisconnectInfo disconnectInfo)
        {
            _playerManager.RemovePlayer(peer);
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
