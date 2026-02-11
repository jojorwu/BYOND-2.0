using System;
using Silk.NET.OpenGL;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;
using System.IO;

namespace Client.Graphics
{
    public class RawTextureData : IDisposable
    {
        public int Width { get; }
        public int Height { get; }
        public Rgba32[] Pixels { get; }

        public RawTextureData(string path)
        {
            using var image = Image.Load<Rgba32>(path);
            Width = image.Width;
            Height = image.Height;
            Pixels = new Rgba32[Width * Height];
            image.CopyPixelDataTo(Pixels);
        }

        public void Dispose() { }
    }

    public class Texture : IDisposable
    {
        public uint Id { get; }
        public int Width { get; }
        public int Height { get; }

        private readonly GL _gl;

        public unsafe Texture(GL gl, RawTextureData data)
        {
            _gl = gl;
            Width = data.Width;
            Height = data.Height;

            Id = _gl.GenTexture();
            _gl.BindTexture(TextureTarget.Texture2D, Id);

            _gl.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureWrapS, (int)GLEnum.Repeat);
            _gl.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureWrapT, (int)GLEnum.Repeat);
            _gl.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureMinFilter, (int)GLEnum.LinearMipmapLinear);
            _gl.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureMagFilter, (int)GLEnum.Linear);

            fixed (void* pData = data.Pixels)
            {
                _gl.TexImage2D(TextureTarget.Texture2D, 0, InternalFormat.Rgba, (uint)Width, (uint)Height, 0, PixelFormat.Rgba, PixelType.UnsignedByte, pData);
            }

            _gl.GenerateMipmap(TextureTarget.Texture2D);
        }

        // Legacy constructor for compatibility if needed, but should be avoided
        public unsafe Texture(GL gl, string path) : this(gl, new RawTextureData(path)) { }

        public void Dispose()
        {
            _gl.DeleteTexture(Id);
        }
    }
}
