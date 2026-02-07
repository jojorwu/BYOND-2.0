using System;
using System.Collections.Generic;
using Client.Graphics;
using Silk.NET.OpenGL;
using Shared.Services;
using Texture = Client.Graphics.Texture;

namespace Client.Assets
{
    public class TextureCache : EngineService, IDisposable
    {
        private readonly Dictionary<string, Texture> Cache = new();
        private GL? _gl;

        public void SetGL(GL gl)
        {
            _gl = gl;
        }

        public Texture GetTexture(string path)
        {
            if (_gl == null) throw new InvalidOperationException("TextureCache not initialized with GL context.");

            if (Cache.TryGetValue(path, out var texture))
            {
                return texture;
            }

            texture = new Texture(_gl, "assets/" + path);
            Cache[path] = texture;
            return texture;
        }

        public override Task StopAsync(CancellationToken cancellationToken)
        {
            Dispose();
            return Task.CompletedTask;
        }

        public void Dispose()
        {
            foreach (var texture in Cache.Values)
            {
                texture.Dispose();
            }
            Cache.Clear();
        }
    }
}
