using Shared.Models;
using Shared.Interfaces;
using Shared.Enums;
using Shared.Operations;
using Shared.Services;
using System;
using System.IO;

namespace Editor
{
    /// <summary>
    /// Provides logic for browsing assets managed by the AssetManager.
    /// </summary>
    public class AssetBrowser
    {
        private readonly AssetManager _assetManager;

        public AssetBrowser(AssetManager assetManager)
        {
            _assetManager = assetManager ?? throw new ArgumentNullException(nameof(assetManager));
        }

        /// <summary>
        /// Gets the paths of all available assets.
        /// </summary>
        /// <returns>An array of asset file paths.</returns>
        public string[] GetAssets()
        {
            return _assetManager.GetAssetPaths();
        }
    }
}
