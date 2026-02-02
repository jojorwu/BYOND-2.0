using Shared.Models;
using Shared.Interfaces;
using Shared.Enums;
using Shared.Operations;
using Shared.Services;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace Shared.Interfaces
{
    /// <summary>
    /// Defines a service for discovering game servers.
    /// </summary>
    public interface IServerDiscoveryService
    {
        /// <summary>
        /// Fetches a list of available game servers.
        /// </summary>
        /// <returns>A collection of server information entries.</returns>
        Task<IEnumerable<ServerInfoEntry>> GetServerListAsync();
    }
}
