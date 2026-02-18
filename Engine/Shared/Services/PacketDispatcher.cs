using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Threading.Tasks;
using Shared.Interfaces;
using Shared.Models;

namespace Shared.Services
{
    public interface IPacketDispatcher
    {
        void RegisterHandler(IPacketHandler handler);
        void UnregisterHandler(byte packetTypeId);
        void AddMiddleware(IPacketMiddleware middleware);
        Task DispatchAsync(INetworkPeer peer, string data);
    }

    public class PacketDispatcher : IPacketDispatcher
    {
        private readonly ConcurrentDictionary<byte, IPacketHandler> _handlers = new();
        private readonly List<IPacketMiddleware> _middleware = new();
        private volatile IPacketMiddleware[] _middlewareCache = Array.Empty<IPacketMiddleware>();
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

        public void AddMiddleware(IPacketMiddleware middleware)
        {
            lock (_middleware)
            {
                _middleware.Add(middleware);
                _middlewareCache = _middleware.ToArray();
            }
        }

        public async Task DispatchAsync(INetworkPeer peer, string data)
        {
            if (string.IsNullOrEmpty(data)) return;

            // Simple protocol: first byte is the packet type ID
            byte typeId = (byte)data[0];

            // Avoid Substring allocation by using Span/Memory if the downstream supports it.
            // For now, keeping Substring but noting it as an architectural point.
            string payload = data.Length > 1 ? data.Substring(1) : string.Empty;

            var context = new PacketContext(peer, typeId, payload);

            // Execute middleware pipeline using lock-free cache
            var middlewareChain = _middlewareCache;
            foreach (var middleware in middlewareChain)
            {
                if (!await middleware.ProcessAsync(context))
                    return; // Middleware aborted the pipeline
            }

            if (_handlers.TryGetValue(context.TypeId, out var handler))
            {
                // Offload packet handling to the JobSystem for parallel processing
                // Use track: false to prevent memory leaks as we don't await these jobs in the game loop
                _jobSystem.Schedule(async () =>
                {
                    await handler.HandleAsync(context.Peer, context.Payload);
                }, track: false);
            }
        }
    }
}
