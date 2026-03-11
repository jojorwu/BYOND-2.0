using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Shared.Interfaces;
using Microsoft.Extensions.Logging;

namespace Shared.Services;
    public class EngineServiceGroup : EngineService, IEngineService
    {
        private readonly string _groupName;
        private readonly List<IEngineService> _services;
        private readonly ILogger? _logger;

        public override string Name => _groupName;
        public override int Priority { get; }
        public override bool IsCritical => _services.Any(s => s.IsCritical);
        public override ServiceStatus Status => _services.Any(s => s.Status == ServiceStatus.Failed) ? ServiceStatus.Failed :
                                      _services.All(s => s.Status == ServiceStatus.Stopped) ? ServiceStatus.Stopped :
                                      ServiceStatus.Running;

        public EngineServiceGroup(string groupName, int priority, IEnumerable<IEngineService> services, ILogger? logger = null)
        {
            _groupName = groupName;
            Priority = priority;
            _services = services.OrderByDescending(s => s.Priority).ToList();
            _logger = logger;
        }

        public override async Task InitializeAsync()
        {
            _logger?.LogInformation("Initializing Service Group: {GroupName}", _groupName);
            foreach (var service in _services)
            {
                await service.InitializeAsync();
            }
        }

        public override async Task StartAsync(CancellationToken cancellationToken)
        {
            _logger?.LogInformation("Starting Service Group: {GroupName}", _groupName);

            // Start services within the group in parallel if they share priority
            var priorityGroups = _services
                .GroupBy(s => s.Priority)
                .OrderByDescending(g => g.Key);

            foreach (var group in priorityGroups)
            {
                await Task.WhenAll(group.Select(s => s.StartAsync(cancellationToken)));
            }
        }

        public override Dictionary<string, object> GetDiagnosticInfo()
        {
            var info = base.GetDiagnosticInfo();
            var serviceStates = _services.ToDictionary(s => s.Name, s => s.Status.ToString());
            info["Services"] = serviceStates;
            return info;
        }

        public override async Task StopAsync(CancellationToken cancellationToken)
        {
            _logger?.LogInformation("Stopping Service Group: {GroupName}", _groupName);

            // Stop in reverse priority
            var priorityGroups = _services
                .GroupBy(s => s.Priority)
                .OrderBy(g => g.Key);

            foreach (var group in priorityGroups)
            {
                await Task.WhenAll(group.Select(s => s.StopAsync(cancellationToken)));
            }
        }
    }
