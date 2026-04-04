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

        public RawTextureData(int width, int height, Rgba32[] pixels)
        {
            Width = width;
            Height = height;
            Pixels = pixels;
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

        public void Dispose()
        {
            _gl.DeleteTexture(Id);
        }
    }

    public class TextureArray : IDisposable
    {
        public uint Id { get; }
        public int Width { get; }
        public int Height { get; }
        public int Depth { get; }

        private readonly GL _gl;

        public unsafe TextureArray(GL gl, int width, int height, int depth)
        {
            _gl = gl;
            Width = width;
            Height = height;
            Depth = depth;

            Id = _gl.GenTexture();
            _gl.BindTexture(TextureTarget.Texture2DArray, Id);

            _gl.TexParameter(TextureTarget.Texture2DArray, TextureParameterName.TextureMinFilter, (int)GLEnum.LinearMipmapLinear);
            _gl.TexParameter(TextureTarget.Texture2DArray, TextureParameterName.TextureMagFilter, (int)GLEnum.Linear);
            _gl.TexParameter(TextureTarget.Texture2DArray, TextureParameterName.TextureWrapS, (int)GLEnum.ClampToEdge);
            _gl.TexParameter(TextureTarget.Texture2DArray, TextureParameterName.TextureWrapT, (int)GLEnum.ClampToEdge);

            _gl.TexImage3D(TextureTarget.Texture2DArray, 0, InternalFormat.Rgba, (uint)width, (uint)height, (uint)depth, 0, PixelFormat.Rgba, PixelType.UnsignedByte, null);
        }

        public unsafe void UpdateLayer(int layer, RawTextureData data)
        {
            if (data.Width != Width || data.Height != Height) throw new ArgumentException("Texture size mismatch");

            _gl.BindTexture(TextureTarget.Texture2DArray, Id);
            fixed (void* pData = data.Pixels)
            {
                _gl.TexSubImage3D(TextureTarget.Texture2DArray, 0, 0, 0, layer, (uint)Width, (uint)Height, 1, PixelFormat.Rgba, PixelType.UnsignedByte, pData);
            }
        }

        public void GenerateMipmaps()
        {
            _gl.BindTexture(TextureTarget.Texture2DArray, Id);
            _gl.GenerateMipmap(TextureTarget.Texture2DArray);
        }

        public void Dispose()
        {
            _gl.DeleteTexture(Id);
        }
    }
}
