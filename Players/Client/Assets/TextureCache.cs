using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Client.Graphics;
using Silk.NET.OpenGL;
using Shared.Services;
using Texture = Client.Graphics.Texture;

namespace Client.Assets
{
    public class TextureCache : EngineService, IDisposable
    {
        private readonly ConcurrentDictionary<string, Texture> Cache = new();
        private GL? _gl;
        private readonly object _glLock = new();

        public void SetGL(GL gl)
        {
            _gl = gl;
        }

        public Texture GetTexture(string path)
        {
            if (_gl == null) throw new InvalidOperationException("TextureCache not initialized with GL context.");

            return Cache.GetOrAdd(path, p =>
            {
                lock (_glLock)
                {
                    return new Texture(_gl, "assets/" + p);
                }
            });
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
