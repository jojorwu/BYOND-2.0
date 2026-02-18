using Shared.Attributes;
using Shared.Interfaces;
using Shared.Models;
using System;
using System.Linq;

namespace Server.Systems
{
    [System("StatusCommandSystem", Priority = 1000)] // Register early
    public class StatusCommandSystem : BaseSystem, ICommandHandler
    {
        private readonly ICommandRegistry _registry;
        private readonly PerformanceMonitor _monitor;

        public string CommandName => "status";
        public string Description => "Shows server status and performance metrics.";

        public StatusCommandSystem(ICommandRegistry registry, PerformanceMonitor monitor)
        {
            _registry = registry;
            _monitor = monitor;
        }

        public override void Initialize()
        {
            _registry.RegisterHandler(this);
        }

        public string Execute(string[] args)
        {
            return "Server Status: Running\n" +
                   $"TPS: {_monitor.LastTps:F1}\n" +
                   $"Memory (GC): {GC.GetTotalMemory(false) / 1024.0 / 1024.0:F1} MB";
        }

        public override void Tick(IEntityCommandBuffer ecb) { }
    }
}
