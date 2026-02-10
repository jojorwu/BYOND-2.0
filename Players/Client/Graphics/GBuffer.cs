using System;
using Silk.NET.OpenGL;

namespace Client.Graphics
{
    public class GBuffer : IDisposable
    {
        private readonly GL _gl;
        public uint Fbo { get; }
        public uint AlbedoTexture { get; }
        public uint NormalTexture { get; }
        public int Width { get; private set; }
        public int Height { get; private set; }

        public GBuffer(GL gl, int width, int height)
        {
            _gl = gl;
            Width = width;
            Height = height;

            Fbo = _gl.GenFramebuffer();
            _gl.BindFramebuffer(FramebufferTarget.Framebuffer, Fbo);

            // Albedo
            AlbedoTexture = _gl.GenTexture();
            _gl.BindTexture(TextureTarget.Texture2D, AlbedoTexture);
            unsafe {
                _gl.TexImage2D(TextureTarget.Texture2D, 0, InternalFormat.Rgba, (uint)width, (uint)height, 0, PixelFormat.Rgba, PixelType.UnsignedByte, null);
            }
            _gl.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureMinFilter, (int)GLEnum.Linear);
            _gl.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureMagFilter, (int)GLEnum.Linear);
            _gl.FramebufferTexture2D(FramebufferTarget.Framebuffer, FramebufferAttachment.ColorAttachment0, TextureTarget.Texture2D, AlbedoTexture, 0);

            // Normals
            NormalTexture = _gl.GenTexture();
            _gl.BindTexture(TextureTarget.Texture2D, NormalTexture);
            unsafe {
                _gl.TexImage2D(TextureTarget.Texture2D, 0, InternalFormat.Rgba, (uint)width, (uint)height, 0, PixelFormat.Rgba, PixelType.UnsignedByte, null);
            }
            _gl.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureMinFilter, (int)GLEnum.Linear);
            _gl.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureMagFilter, (int)GLEnum.Linear);
            _gl.FramebufferTexture2D(FramebufferTarget.Framebuffer, FramebufferAttachment.ColorAttachment1, TextureTarget.Texture2D, NormalTexture, 0);

            var attachments = new GLEnum[] { GLEnum.ColorAttachment0, GLEnum.ColorAttachment1 };
            unsafe {
                fixed (GLEnum* a = attachments)
                    _gl.DrawBuffers(2, a);
            }

            if (_gl.CheckFramebufferStatus(FramebufferTarget.Framebuffer) != GLEnum.FramebufferComplete)
                throw new Exception("GBuffer is not complete!");

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

            _gl.BindTexture(TextureTarget.Texture2D, AlbedoTexture);
            unsafe {
                _gl.TexImage2D(TextureTarget.Texture2D, 0, InternalFormat.Rgba, (uint)width, (uint)height, 0, PixelFormat.Rgba, PixelType.UnsignedByte, null);
            }
            _gl.BindTexture(TextureTarget.Texture2D, NormalTexture);
            unsafe {
                _gl.TexImage2D(TextureTarget.Texture2D, 0, InternalFormat.Rgba, (uint)width, (uint)height, 0, PixelFormat.Rgba, PixelType.UnsignedByte, null);
            }
        }

        public void Dispose()
        {
            _gl.DeleteFramebuffer(Fbo);
            _gl.DeleteTexture(AlbedoTexture);
            _gl.DeleteTexture(NormalTexture);
        }
    }
}
