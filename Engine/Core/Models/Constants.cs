using Shared.Models;
using Shared.Interfaces;
using Shared.Enums;
using Shared.Operations;
using Shared.Services;
namespace Core
{
    /// <summary>
    /// Defines global constants for the application.
    /// </summary>
    public static class Constants
    {
        /// <summary>
        /// The root directory for all game scripts.
        /// </summary>
        public static readonly string ScriptsRoot = "scripts";

        /// <summary>
        /// The root directory for all map files.
        /// </summary>
        public static readonly string MapsRoot = "maps";
    }
}
