using System;
using System.Collections.Generic;
using Client.Graphics;
using Silk.NET.OpenGL;
using Texture = Client.Graphics.Texture;

namespace Client.Assets
{
    public static class TextureCache
    {
        private static readonly Dictionary<string, Texture> Cache = new();
        private static GL _gl;

        public static void Init(GL gl)
        {
            _gl = gl;
        }

        public static Texture GetTexture(string path)
        {
            if (Cache.TryGetValue(path, out var texture))
            {
                return texture;
            }

            texture = new Texture(_gl, "assets/" + path);
            Cache[path] = texture;
            return texture;
        }

        public static void Dispose()
        {
            foreach (var texture in Cache.Values)
            {
                texture.Dispose();
            }
            Cache.Clear();
        }
    }
}
