using Shared.Models;
using Shared.Interfaces;
using Shared.Enums;
using Shared.Operations;
using Shared.Services;
using Silk.NET.OpenGL;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;
using SixLabors.ImageSharp.Processing;
using System;
using System.IO;

namespace Launcher
{
    public class Texture : IDisposable
    {
        private readonly GL _gl;
        public readonly uint Handle;
        public readonly int Width;
        public readonly int Height;

        public Texture(GL gl, string path)
        {
            _gl = gl;

            if (!File.Exists(path))
            {
                throw new FileNotFoundException("Texture file not found.", path);
            }

            using (var img = Image.Load<Rgba32>(path))
            {
                Width = img.Width;
                Height = img.Height;

                // ImageSharp loads images upside down, so we need to flip them
                img.Mutate(x => x.Flip(FlipMode.Vertical));

                Handle = _gl.GenTexture();
                Bind();

                unsafe
                {
                    // Copy the pixel data to a byte array
                    var pixels = new byte[Width * Height * 4];
                    img.CopyPixelDataTo(pixels);

                    fixed (void* data = pixels)
                    {
                        _gl.TexImage2D(TextureTarget.Texture2D, 0, InternalFormat.Rgba, (uint)Width, (uint)Height, 0, PixelFormat.Rgba, PixelType.UnsignedByte, data);
                    }
                }

                SetParameters();
            }
        }

        private void SetParameters()
        {
            _gl.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureWrapS, (int)TextureWrapMode.ClampToEdge);
            _gl.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureWrapT, (int)TextureWrapMode.ClampToEdge);
            _gl.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureMinFilter, (int)TextureMinFilter.Linear);
            _gl.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureMagFilter, (int)TextureMagFilter.Linear);
        }

        public void Bind(TextureUnit unit = TextureUnit.Texture0)
        {
            _gl.ActiveTexture(unit);
            _gl.BindTexture(TextureTarget.Texture2D, Handle);
        }

        public void Dispose()
        {
            _gl.DeleteTexture(Handle);
        }
    }
}
