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
        private readonly GL _gl;
        private readonly Dictionary<string, uint> _textureCache = new Dictionary<string, uint>();

        public TextureManager(GL gl)
        {
            _gl = gl;
        }

        public uint GetTexture(string assetPath)
        {
            if (string.IsNullOrEmpty(assetPath) || !File.Exists(assetPath))
            {
                return 0; // Return an invalid texture ID
            }

            if (_textureCache.TryGetValue(assetPath, out uint textureId))
            {
                return textureId;
            }

            // Load the image with ImageSharp
            using (var image = Image.Load<Rgba32>(assetPath))
            {
                // Generate a new OpenGL texture
                uint newTextureId = _gl.GenTexture();
                _gl.BindTexture(TextureTarget.Texture2D, newTextureId);

                // Set texture parameters
                _gl.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureWrapS, (int)GLEnum.Repeat);
                _gl.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureWrapT, (int)GLEnum.Repeat);
                _gl.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureMinFilter, (int)GLEnum.LinearMipmapLinear);
                _gl.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureMagFilter, (int)GLEnum.Linear);

                // Upload the image data to the texture
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
                     _gl.TexImage2D(TextureTarget.Texture2D, 0, InternalFormat.Rgba, (uint)accessor.Width, (uint)accessor.Height, 0, PixelFormat.Rgba, PixelType.UnsignedByte, data);
                });

                _gl.GenerateMipmap(TextureTarget.Texture2D);

                _textureCache[assetPath] = newTextureId;
                return newTextureId;
            }
        }

        public void Dispose()
        {
            foreach (var textureId in _textureCache.Values)
            {
                _gl.DeleteTexture(textureId);
            }
            _textureCache.Clear();
        }
    }
}
