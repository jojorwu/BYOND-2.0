using Shared.Models;
using Shared.Interfaces;
using Shared.Enums;
using Shared.Operations;
using Shared.Services;
using System.Collections.Generic;
using System.IO;
using Client.Graphics;
using Core.Dmi;

namespace Client.Assets
{
    public static class DmiCache
    {
        private static readonly Dictionary<string, DmiAsset> Cache = new();

        public static DmiAsset GetDmi(string path, Texture texture)
        {
            if (Cache.TryGetValue(path, out var dmi))
            {
                return dmi;
            }

            var dmiDescription = DmiParser.ParseDMI(File.OpenRead("assets/" + path));
            var dmiAsset = new DmiAsset(texture.Id, texture.Width, texture.Height, dmiDescription);
            Cache[path] = dmiAsset;
            return dmiAsset;
        }
    }
}
