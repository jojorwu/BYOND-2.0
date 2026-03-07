using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Shared.Interfaces;
using Microsoft.Extensions.Logging;

namespace Shared.Services;
    public class EngineServiceGroup : IEngineService
    {
        private readonly string _groupName;
        private readonly List<IEngineService> _services;
        private readonly ILogger? _logger;

        public int Priority { get; }
        private ServiceStatus _status = ServiceStatus.Stopped;
        public ServiceStatus Status => _status;

        public EngineServiceGroup(string groupName, int priority, IEnumerable<IEngineService> services, ILogger? logger = null)
        {
            _groupName = groupName;
            Priority = priority;
            _services = services.OrderByDescending(s => s.Priority).ToList();
            _logger = logger;
        }

        public async Task InitializeAsync()
        {
            _logger?.LogInformation("Initializing Service Group: {GroupName}", _groupName);
            foreach (var service in _services)
            {
                await service.InitializeAsync();
            }
        }

        public async Task StartAsync(CancellationToken cancellationToken)
        {
            _status = ServiceStatus.Starting;
            _logger?.LogInformation("Starting Service Group: {GroupName}", _groupName);

            // Start services within the group in parallel if they share priority
            var priorityGroups = _services
                .GroupBy(s => s.Priority)
                .OrderByDescending(g => g.Key);

            foreach (var group in priorityGroups)
            {
                await Task.WhenAll(group.Select(s => s.StartAsync(cancellationToken)));
            }
            _status = ServiceStatus.Running;
        }

        public async Task StopAsync(CancellationToken cancellationToken)
        {
            _status = ServiceStatus.Stopping;
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
