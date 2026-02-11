using System;
using Silk.NET.OpenGL;

namespace Client.Graphics
{
    public class Framebuffer : IDisposable
    {
        private readonly GL _gl;
        public uint Fbo { get; }
        public uint Texture { get; }
        public int Width { get; private set; }
        public int Height { get; private set; }

        public Framebuffer(GL gl, int width, int height)
        {
            _gl = gl;
            Width = width;
            Height = height;

            Fbo = _gl.GenFramebuffer();
            _gl.BindFramebuffer(FramebufferTarget.Framebuffer, Fbo);

            Texture = _gl.GenTexture();
            _gl.BindTexture(TextureTarget.Texture2D, Texture);
            unsafe {
                _gl.TexImage2D(TextureTarget.Texture2D, 0, InternalFormat.Rgba, (uint)width, (uint)height, 0, PixelFormat.Rgba, PixelType.UnsignedByte, null);
            }
            _gl.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureMinFilter, (int)GLEnum.Linear);
            _gl.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureMagFilter, (int)GLEnum.Linear);

            _gl.FramebufferTexture2D(FramebufferTarget.Framebuffer, FramebufferAttachment.ColorAttachment0, TextureTarget.Texture2D, Texture, 0);

            if (_gl.CheckFramebufferStatus(FramebufferTarget.Framebuffer) != GLEnum.FramebufferComplete)
                throw new Exception("Framebuffer is not complete!");

            _gl.BindFramebuffer(FramebufferTarget.Framebuffer, 0);
        }

        public void Bind()
        {
            _gl.BindFramebuffer(FramebufferTarget.Framebuffer, Fbo);
            _gl.Viewport(0, 0, (uint)Width, (uint)Height);
        }

        public void Unbind()
        {
            _gl.BindFramebuffer(FramebufferTarget.Framebuffer, 0);
        }

        public void Resize(int width, int height)
        {
            if (width == Width && height == Height) return;
            Width = width;
            Height = height;

            _gl.BindTexture(TextureTarget.Texture2D, Texture);
            unsafe {
                _gl.TexImage2D(TextureTarget.Texture2D, 0, InternalFormat.Rgba, (uint)width, (uint)height, 0, PixelFormat.Rgba, PixelType.UnsignedByte, null);
            }
        }

        public void BlitTo(Framebuffer other)
        {
            _gl.BindFramebuffer(FramebufferTarget.ReadFramebuffer, Fbo);
            _gl.BindFramebuffer(FramebufferTarget.DrawFramebuffer, other.Fbo);
            _gl.BlitFramebuffer(0, 0, Width, Height, 0, 0, other.Width, other.Height, ClearBufferMask.ColorBufferBit, BlitFramebufferFilter.Nearest);
            _gl.BindFramebuffer(FramebufferTarget.Framebuffer, 0);
        }

        public void Dispose()
        {
            _gl.DeleteFramebuffer(Fbo);
            _gl.DeleteTexture(Texture);
        }
    }
}
