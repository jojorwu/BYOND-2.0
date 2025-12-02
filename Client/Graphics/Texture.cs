using Silk.NET.OpenGL;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;
using System;
using System.IO;

namespace Client.Graphics
{
    public class Texture : IDisposable
    {
        public uint Id { get; }
        public int Width { get; }
        public int Height { get; }

        private readonly GL _gl;

        public unsafe Texture(GL gl, string path)
        {
            _gl = gl;

            using (var image = Image.Load<Rgba32>(path))
            {
                Width = image.Width;
                Height = image.Height;

                Id = _gl.GenTexture();
                _gl.BindTexture(TextureTarget.Texture2D, Id);

                _gl.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureWrapS, (int)GLEnum.Repeat);
                _gl.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureWrapT, (int)GLEnum.Repeat);
                _gl.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureMinFilter, (int)GLEnum.LinearMipmapLinear);
                _gl.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureMagFilter, (int)GLEnum.Linear);

                fixed (void* data = image.GetPixelRowSpan(0))
                {
                    _gl.TexImage2D(TextureTarget.Texture2D, 0, InternalFormat.Rgba, (uint)Width, (uint)Height, 0, PixelFormat.Rgba, PixelType.UnsignedByte, data);
                }

                _gl.GenerateMipmap(TextureTarget.Texture2D);
            }
        }

        public void Dispose()
        {
            _gl.DeleteTexture(Id);
        }
    }
}
