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
        private readonly ConcurrentDictionary<string, Task<RawTextureData>> PendingData = new();
        private readonly ConcurrentQueue<Action> UploadQueue = new();

        private GL? _gl;

        public void SetGL(GL gl)
        {
            _gl = gl;
        }

        public Texture? GetTexture(string path)
        {
            if (_gl == null) throw new InvalidOperationException("TextureCache not initialized with GL context.");

            if (Cache.TryGetValue(path, out var texture))
            {
                return texture;
            }

            // Start loading data if not already loading
            if (!PendingData.ContainsKey(path))
            {
                var loadTask = Task.Run(() => new RawTextureData("assets/" + path));
                if (PendingData.TryAdd(path, loadTask))
                {
                    loadTask.ContinueWith(t =>
                    {
                        if (t.IsCompletedSuccessfully)
                        {
                            UploadQueue.Enqueue(() =>
                            {
                                var tex = new Texture(_gl, t.Result);
                                Cache.TryAdd(path, tex);
                                PendingData.TryRemove(path, out _);
                                t.Result.Dispose();
                            });
                        }
                    });
                }
            }

            return null; // Return null if not ready, will be loaded eventually
        }

        /// <summary>
        /// Processes the upload queue on the main thread (where GL context is active).
        /// </summary>
        public void ProcessUploads()
        {
            while (UploadQueue.TryDequeue(out var uploadAction))
            {
                uploadAction();
            }
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
            PendingData.Clear();
        }
    }
}
