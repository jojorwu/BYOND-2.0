using System.Collections.Generic;
using System.IO;
using Client.Graphics;
using Core.Dmi;
using Shared.Services;

namespace Client.Assets
{
    public class DmiCache : EngineService
    {
        private readonly Dictionary<string, DmiAsset> Cache = new();

        public DmiAsset GetDmi(string path, Texture texture)
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
