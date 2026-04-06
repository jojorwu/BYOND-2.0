using System;
using Silk.NET.OpenGL;
using Shared.Services;
using Client.Assets;

namespace Client.Graphics
{
    /// <summary>
    /// Consolidates graphics resource management (Textures, DMIs, Icons).
    /// Orchestrates the loading and lifecycle of assets for the rendering pipeline.
    /// </summary>
    public class GraphicsResourceManager : EngineService, IDisposable
    {
        private readonly TextureCache _textureCache;
        private readonly DmiCache _dmiCache;
        private readonly IconCache _iconCache;

        public TextureCache TextureCache => _textureCache;
        public DmiCache DmiCache => _dmiCache;
        public IconCache IconCache => _iconCache;

        public GraphicsResourceManager(TextureCache textureCache, DmiCache dmiCache, IconCache iconCache)
        {
            _textureCache = textureCache;
            _dmiCache = dmiCache;
            _iconCache = iconCache;
        }

        public void Initialize(GL gl)
        {
            _textureCache.SetGL(gl);
        }

        public void ProcessUploads()
        {
            _textureCache.ProcessUploads();
        }

        public void Dispose()
        {
            _textureCache.Dispose();
        }
    }
}
