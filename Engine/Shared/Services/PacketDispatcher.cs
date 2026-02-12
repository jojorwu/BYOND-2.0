using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Threading.Tasks;
using Shared.Interfaces;

namespace Shared.Services
{
    public interface IPacketDispatcher
    {
        void RegisterHandler(IPacketHandler handler);
        void UnregisterHandler(byte packetTypeId);
        Task DispatchAsync(INetworkPeer peer, string data);
    }

    public class PacketDispatcher : IPacketDispatcher
    {
        private readonly ConcurrentDictionary<byte, IPacketHandler> _handlers = new();
        private readonly IJobSystem _jobSystem;

        public PacketDispatcher(IJobSystem jobSystem)
        {
            _jobSystem = jobSystem;
        }

        public void RegisterHandler(IPacketHandler handler)
        {
            _handlers[handler.PacketTypeId] = handler;
        }

        public void UnregisterHandler(byte packetTypeId)
        {
            _handlers.TryRemove(packetTypeId, out _);
        }

        public async Task DispatchAsync(INetworkPeer peer, string data)
        {
            if (string.IsNullOrEmpty(data)) return;

            // Simple protocol: first byte is the packet type ID
            byte typeId = (byte)data[0];
            string payload = data.Substring(1);

            if (_handlers.TryGetValue(typeId, out var handler))
            {
                // Offload packet handling to the JobSystem for parallel processing
                _jobSystem.Schedule(async () =>
                {
                    await handler.HandleAsync(peer, payload);
                });
            }
        }
    }
}
