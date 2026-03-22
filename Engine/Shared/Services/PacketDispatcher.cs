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
        private readonly System.Threading.Lock _middlewareLock = new();
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
            using (_middlewareLock.EnterScope())
            {
                _middleware.Add(middleware);
                _middlewareCache = _middleware.ToArray();
            }
        }

        public async Task DispatchAsync(INetworkPeer peer, string data)
        {
            if (string.IsNullOrEmpty(data)) return;

            int maxByteCount = System.Text.Encoding.UTF8.GetMaxByteCount(data.Length);
            var buffer = System.Buffers.ArrayPool<byte>.Shared.Rent(maxByteCount);
            try
            {
                int actualByteCount = System.Text.Encoding.UTF8.GetBytes(data, buffer);
                await DispatchAsync(peer, new ReadOnlyMemory<byte>(buffer, 0, actualByteCount));
            }
            finally
            {
                System.Buffers.ArrayPool<byte>.Shared.Return(buffer);
            }
        }

        public async Task DispatchAsync(INetworkPeer peer, ReadOnlyMemory<byte> data)
        {
            if (data.IsEmpty) return;

            byte typeId = data.Span[0];
            var payload = data.Slice(1);
            var context = new PacketContext(peer, typeId, payload);

            var middlewareChain = _middlewareCache;
            for (int i = 0; i < middlewareChain.Length; i++)
            {
                var result = await middlewareChain[i].ProcessAsync(context);
                if (!result)
                    return;
            }

            if (_handlers.TryGetValue(context.TypeId, out var handler))
            {
                var payloadLength = context.Payload.Length;
                var buffer = System.Buffers.ArrayPool<byte>.Shared.Rent(payloadLength);
                context.Payload.CopyTo(buffer);
                var payloadCopy = new ReadOnlyMemory<byte>(buffer, 0, payloadLength);

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
