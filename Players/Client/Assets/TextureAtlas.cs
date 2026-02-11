using System;
using System.Collections.Generic;
using Silk.NET.OpenGL;
using Client.Graphics;
using SixLabors.ImageSharp.PixelFormats;
using Robust.Shared.Maths;

namespace Client.Assets
{
    public class TextureAtlas : IDisposable
    {
        private const int AtlasSize = 2048;
        private const int TileSize = 32;
        private const int TilesPerRow = AtlasSize / TileSize;

        private readonly GL _gl;
        public uint TextureId { get; }
        private readonly bool[] _usedTiles = new bool[TilesPerRow * TilesPerRow];
        private readonly Dictionary<string, int> _tileIndices = new();

        public TextureAtlas(GL gl)
        {
            _gl = gl;
            TextureId = _gl.GenTexture();
            _gl.BindTexture(TextureTarget.Texture2D, TextureId);

            unsafe
            {
                _gl.TexImage2D(TextureTarget.Texture2D, 0, InternalFormat.Rgba, AtlasSize, AtlasSize, 0, PixelFormat.Rgba, PixelType.UnsignedByte, null);
            }

            _gl.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureMinFilter, (int)GLEnum.Linear);
            _gl.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureMagFilter, (int)GLEnum.Linear);
        }

        public unsafe bool TryInsert(string id, RawTextureData data, out Box2 uv)
        {
            if (data.Width != TileSize || data.Height != TileSize)
            {
                uv = default;
                return false;
            }

            if (_tileIndices.TryGetValue(id, out int index))
            {
                uv = GetUv(index);
                return true;
            }

            for (int i = 0; i < _usedTiles.Length; i++)
            {
                if (!_usedTiles[i])
                {
                    _usedTiles[i] = true;
                    _tileIndices[id] = i;

                    int x = (i % TilesPerRow) * TileSize;
                    int y = (i / TilesPerRow) * TileSize;

                    _gl.BindTexture(TextureTarget.Texture2D, TextureId);
                    fixed (void* p = data.Pixels)
                    {
                        _gl.TexSubImage2D(TextureTarget.Texture2D, 0, x, y, TileSize, TileSize, PixelFormat.Rgba, PixelType.UnsignedByte, p);
                    }

                    uv = GetUv(i);
                    return true;
                }
            }

            uv = default;
            return false;
        }

        private Box2 GetUv(int index)
        {
            float x = (index % TilesPerRow) * TileSize;
            float y = (index / TilesPerRow) * TileSize;
            return new Box2(x / AtlasSize, y / AtlasSize, (x + TileSize) / AtlasSize, (y + TileSize) / AtlasSize);
        }

        public void Dispose()
        {
            _gl.DeleteTexture(TextureId);
        }
    }
}
