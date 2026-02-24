using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Threading.Tasks;
using Shared.Interfaces;
using Shared.Models;

namespace Shared.Services;
    public interface IPacketDispatcher
    {
        void RegisterHandler(IPacketHandler handler);
        void UnregisterHandler(byte packetTypeId);
        void AddMiddleware(IPacketMiddleware middleware);
        Task DispatchAsync(INetworkPeer peer, string data);
        Task DispatchAsync(INetworkPeer peer, ReadOnlyMemory<byte> data);
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
            await DispatchAsync(peer, System.Text.Encoding.UTF8.GetBytes(data).AsMemory());
        }

        public async Task DispatchAsync(INetworkPeer peer, ReadOnlyMemory<byte> data)
        {
            if (data.IsEmpty) return;

            // Simple protocol: first byte is the packet type ID
            byte typeId = data.Span[0];
            var payload = data.Slice(1);

            // Note: PacketContext might need to be updated to support binary payloads.
            // For now, if we have handlers that expect string, we convert.
            // Architectural goal: move all handlers to binary.

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
                // Offload packet handling to the JobSystem for parallel processing.
                // We copy the payload because the underlying network buffer might be reused.
                var buffer = System.Buffers.ArrayPool<byte>.Shared.Rent(context.Payload.Length);
                context.Payload.CopyTo(buffer);
                var payloadCopy = new ReadOnlyMemory<byte>(buffer, 0, context.Payload.Length);

                _jobSystem.Schedule(async () =>
                {
                    try
                    {
                        await handler.HandleAsync(context.Peer, payloadCopy);
                    }
                    finally
                    {
                        System.Buffers.ArrayPool<byte>.Shared.Return(buffer);
                    }
                }, track: false);
            }
        }
    }
