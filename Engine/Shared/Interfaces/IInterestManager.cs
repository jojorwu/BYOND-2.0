using System.Collections.Generic;
using Shared.Interfaces;

namespace Shared.Interfaces
{
    /// <summary>
    /// Manages the Area of Interest (AOI) for players, determining which objects are visible to them.
    /// </summary>
    public interface IInterestManager
    {
        /// <summary>
        /// Updates the interest state for a player based on their current position and visibility range.
        /// </summary>
        /// <param name="peer">The network peer (player).</param>
        /// <param name="x">The player's X coordinate.</param>
        /// <param name="y">The player's Y coordinate.</param>
        /// <param name="range">The visibility range (radius).</param>
        void UpdatePlayerInterest(INetworkPeer peer, int x, int y, int range);

        /// <summary>
        /// Gets the set of game objects currently interesting to the player.
        /// </summary>
        /// <param name="peer">The network peer.</param>
        /// <returns>A collection of objects within the player's interest range.</returns>
        IEnumerable<IGameObject> GetInterestedObjects(INetworkPeer peer);

        /// <summary>
        /// Clears interest data for a player when they disconnect.
        /// </summary>
        /// <param name="peer">The network peer.</param>
        void ClearPlayerInterest(INetworkPeer peer);
    }
}
