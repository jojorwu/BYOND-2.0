using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Threading.Tasks;
using Shared.Interfaces;
using Shared.Models;

namespace Shared.Services
{
    /// <summary>
    /// Dispatches incoming network packets to the appropriate handlers after passing them through a middleware pipeline.
    /// </summary>
    public interface IPacketDispatcher
    {
        /// <summary>
        /// Registers a handler for a specific packet type.
        /// </summary>
        void RegisterHandler(IPacketHandler handler);

        /// <summary>
        /// Unregisters a handler for a specific packet type.
        /// </summary>
        void UnregisterHandler(byte packetTypeId);

        /// <summary>
        /// Adds middleware to the packet processing pipeline.
        /// </summary>
        void AddMiddleware(IPacketMiddleware middleware);

        /// <summary>
        /// Dispatches a raw packet string to the middleware and handlers.
        /// </summary>
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

            // Simple protocol: first character is the packet type ID
            byte typeId = (byte)data[0];

            // If the data is large, Substring is expensive.
            // In the future, we should migrate PacketContext to use ReadOnlyMemory<char>.
            string payload = data.Length > 1 ? data[1..] : string.Empty;

            var context = new PacketContext(peer, typeId, payload);

            // Execute middleware pipeline using lock-free cache snapshot
            var middlewareChain = _middlewareCache;
            foreach (var middleware in middlewareChain)
            {
                try
                {
                    if (!await middleware.ProcessAsync(context))
                        return; // Middleware aborted the pipeline
                }
                catch (Exception)
                {
                    // If middleware fails, we typically want to abort processing this packet for safety
                    return;
                }
            }

            if (_handlers.TryGetValue(context.TypeId, out var handler))
            {
                // Offload packet handling to the JobSystem for parallel processing
                // Use track: false to prevent memory leaks as we don't await these jobs in the game loop
                _jobSystem.Schedule(async () =>
                {
                    try
                    {
                        await handler.HandleAsync(context.Peer, context.Payload);
                    }
                    catch (Exception ex)
                    {
                        // Log handling errors but don't crash the worker thread
                        Console.Error.WriteLine($"[PacketDispatcher] Error handling packet {context.TypeId}: {ex.Message}");
                    }
                }, track: false, priority: JobPriority.High);
            }
        }
    }
}
