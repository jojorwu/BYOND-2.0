using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using Client.Graphics;
using Core.Dmi;
using Shared.Services;

namespace Client.Assets
{
    public class DmiCache : EngineService
    {
        private readonly ConcurrentDictionary<string, Lazy<DmiAsset>> Cache = new();

        public DmiAsset? GetDmi(string path, Texture? texture)
        {
            if (texture == null) return null;

            var lazy = Cache.GetOrAdd(path, p => new Lazy<DmiAsset>(() =>
            {
                using var stream = File.OpenRead("assets/" + p);
                var dmiDescription = DmiParser.ParseDMI(stream);
                return new DmiAsset(texture.Id, texture.Width, texture.Height, dmiDescription);
            }));

            return lazy.Value;
        }
    }
}
