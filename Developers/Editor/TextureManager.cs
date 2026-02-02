using Shared.Models;
using Shared.Interfaces;
using Shared.Enums;
using Shared.Operations;
using Shared.Services;
using Silk.NET.OpenGL;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;
using System;
using System.Collections.Generic;
using System.IO;

namespace Editor
{
    public class TextureManager : IDisposable
    {
        private GL? _gl;
        private readonly Dictionary<string, uint> _textureCache = new Dictionary<string, uint>();

        public TextureManager()
        {
        }

        public void Initialize(GL gl)
        {
            _gl = gl;
        }

        public uint GetTexture(string assetPath)
        {
             if (string.IsNullOrEmpty(assetPath) || !File.Exists(assetPath) || _gl == null)
            {
                return 0;
            }

            if (_textureCache.TryGetValue(assetPath, out uint textureId))
            {
                return textureId;
            }

            using (var image = Image.Load<Rgba32>(assetPath))
            {
                uint newTextureId = _gl.GenTexture();
                _gl.BindTexture(TextureTarget.Texture2D, newTextureId);

                _gl.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureWrapS, (int)GLEnum.Repeat);
                _gl.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureWrapT, (int)GLEnum.Repeat);
                _gl.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureMinFilter, (int)GLEnum.LinearMipmapLinear);
                _gl.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureMagFilter, (int)GLEnum.Linear);

                image.ProcessPixelRows(accessor =>
                {
                    var data = new byte[accessor.Width * accessor.Height * 4];
                    for (int y = 0; y < accessor.Height; y++)
                    {
                        var pixelRow = accessor.GetRowSpan(y);
                        for (int x = 0; x < accessor.Width; x++)
                        {
                            var pixel = pixelRow[x];
                            int index = (y * accessor.Width + x) * 4;
                            data[index] = pixel.R;
                            data[index + 1] = pixel.G;
                            data[index + 2] = pixel.B;
                            data[index + 3] = pixel.A;
                        }
                    }
                    unsafe
                    {
                        fixed (byte* ptr = data)
                        {
                            _gl.TexImage2D(TextureTarget.Texture2D, 0, InternalFormat.Rgba, (uint)accessor.Width, (uint)accessor.Height, 0, PixelFormat.Rgba, PixelType.UnsignedByte, ptr);
                        }
                    }
                });

                _gl.GenerateMipmap(TextureTarget.Texture2D);

                _textureCache[assetPath] = newTextureId;
                return newTextureId;
            }
        }

        public void Dispose()
        {
            if (_gl == null) return;
            foreach (var textureId in _textureCache.Values)
            {
                _gl.DeleteTexture(textureId);
            }
            _textureCache.Clear();
        }
    }
}
