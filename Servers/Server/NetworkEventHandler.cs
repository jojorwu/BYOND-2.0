using System;
using System.Text.Json;
using LiteNetLib;
using Shared;
using Server.Events;
using Shared.Messaging;

namespace Server
{
    public class NetworkEventHandler
    {
        private readonly IEventBus _eventBus;
        private readonly IServerContext _context;
        private readonly IScriptHost _scriptHost;

        public NetworkEventHandler(IEventBus eventBus, IServerContext context, IScriptHost scriptHost)
        {
            _eventBus = eventBus;
            _context = context;
            _scriptHost = scriptHost;
        }

        public void SubscribeToEvents()
        {
            _eventBus.Subscribe<PeerConnectedEvent>(OnPeerConnected);
            _eventBus.Subscribe<PeerDisconnectedEvent>(OnPeerDisconnected);
            _eventBus.Subscribe<CommandReceivedEvent>(OnCommandReceived);
        }

        private void OnPeerConnected(PeerConnectedEvent e)
        {
            var peer = e.Peer;
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
        }

        private void OnPeerDisconnected(PeerDisconnectedEvent e)
        {
            _context.PlayerManager.RemovePlayer(e.Peer);
            _context.InterestManager.ClearPlayerInterest(e.Peer);
        }

        private void OnCommandReceived(CommandReceivedEvent e)
        {
            _scriptHost.EnqueueCommand(e.Command, (result) => {
                _ = e.Peer.SendAsync(result);
            });
        }

        public void UnsubscribeFromEvents()
        {
            _eventBus.Unsubscribe<PeerConnectedEvent>(OnPeerConnected);
            _eventBus.Unsubscribe<PeerDisconnectedEvent>(OnPeerDisconnected);
            _eventBus.Unsubscribe<CommandReceivedEvent>(OnCommandReceived);
        }
    }
}
