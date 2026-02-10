using System;
using Silk.NET.OpenGL;
using Robust.Shared.Maths;
using System.Numerics;

namespace Client.Graphics
{
    public class OccluderMap : IDisposable
    {
        private readonly GL _gl;
        public Framebuffer Framebuffer { get; }

        public OccluderMap(GL gl, int width, int height)
        {
            _gl = gl;
            // Use a smaller resolution for the occluder map for performance
            Framebuffer = new Framebuffer(_gl, width / 2, height / 2);
        }

        public void Bind() => Framebuffer.Bind();
        public void Unbind() => Framebuffer.Unbind();
        public uint Texture => Framebuffer.Texture;

        public void Dispose()
        {
            Framebuffer.Dispose();
        }
    }
}
