using System;
using System.Text.Json;
using LiteNetLib;
using Shared;
using Microsoft.Extensions.Logging;

namespace Server
{
    using Shared.Config;

    public class NetworkEventHandler
    {
        private readonly INetworkService _networkService;
        private readonly IServerContext _context;
        private readonly IScriptHost _scriptHost;
        private readonly IUdpServer _udpServer;
        private readonly IConsoleCommandManager _commandManager;
        private readonly ILogger<NetworkEventHandler> _logger;

        public NetworkEventHandler(INetworkService networkService, IServerContext context, IScriptHost scriptHost, IUdpServer udpServer, IConsoleCommandManager commandManager, ILogger<NetworkEventHandler> logger)
        {
            _networkService = networkService;
            _context = context;
            _scriptHost = scriptHost;
            _udpServer = udpServer;
            _commandManager = commandManager;
            _logger = logger;
        }

        public void SubscribeToEvents()
        {
            _networkService.PeerConnected += OnPeerConnected;
            _networkService.PeerDisconnected += OnPeerDisconnected;
        }

        private void OnPeerConnected(INetworkPeer peer)
        {
            _logger.LogInformation("Player connected: {Nickname} ({Address})", peer.Nickname ?? "Unknown", peer.EndPoint);
            _context.PlayerManager.AddPlayer(peer);

            var serverInfo = new ServerInfo
            {
                ServerName = _context.Settings.ServerName,
                ServerDescription = _context.Settings.ServerDescription,
                MaxPlayers = _context.Settings.MaxPlayers,
                AssetUrl = $"http://{_context.Settings.Network.IpAddress}:{_context.Settings.HttpServer.Port}"
            };

            var json = JsonSerializer.Serialize(serverInfo);
            _ = peer.SendAsync(json);

            // Sync Replicated CVars
            _udpServer.SendCVars(peer);
        }

        private void OnPeerDisconnected(INetworkPeer peer, DisconnectInfo disconnectInfo)
        {
            _context.PlayerManager.RemovePlayer(peer);
            _context.InterestManager.ClearPlayerInterest(peer);
        }

        private void OnCommandReceived(INetworkPeer peer, string command)
        {
            Task.Run(async () => {
                var result = await _commandManager.ExecuteCommand(command);
                _ = peer.SendAsync(result);
            });
        }

        public void UnsubscribeFromEvents()
        {
            _networkService.PeerConnected -= OnPeerConnected;
            _networkService.PeerDisconnected -= OnPeerDisconnected;
        }
    }
}
