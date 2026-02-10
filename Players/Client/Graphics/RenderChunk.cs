using System;
using System.Collections.Generic;
using Silk.NET.OpenGL;
using System.Numerics;
using System.Runtime.InteropServices;
using Robust.Shared.Maths;

namespace Client.Graphics
{
    public class RenderChunk : IDisposable
    {
        public const int ChunkSize = 32; // 32x32 tiles

        private readonly GL _gl;
        private readonly uint _vao;
        private readonly uint _vbo;
        private int _vertexCount = 0;
        private bool _isDirty = true;

        public Vector2i Coords { get; }
        public bool IsDirty => _isDirty;

        public RenderChunk(GL gl, Vector2i coords)
        {
            _gl = gl;
            Coords = coords;

            _vao = _gl.GenVertexArray();
            _vbo = _gl.GenBuffer();

            _gl.BindVertexArray(_vao);
            _gl.BindBuffer(BufferTargetARB.ArrayBuffer, _vbo);

            unsafe
            {
                var size = (uint)sizeof(Vertex);
                _gl.EnableVertexAttribArray(0); // Position
                _gl.VertexAttribPointer(0, 2, VertexAttribPointerType.Float, false, size, (void*)0);

                _gl.EnableVertexAttribArray(1); // TexCoords
                _gl.VertexAttribPointer(1, 2, VertexAttribPointerType.Float, false, size, (void*)Marshal.OffsetOf<Vertex>("TexCoords"));

                _gl.EnableVertexAttribArray(2); // Color
                _gl.VertexAttribPointer(2, 4, VertexAttribPointerType.Float, false, size, (void*)Marshal.OffsetOf<Vertex>("Color"));
            }

            _gl.BindVertexArray(0);
        }

        public void MarkDirty()
        {
            _isDirty = true;
        }

        public unsafe void Update(ReadOnlySpan<Vertex> vertices)
        {
            _gl.BindBuffer(BufferTargetARB.ArrayBuffer, _vbo);
            fixed (Vertex* p = vertices)
            {
                _gl.BufferData(BufferTargetARB.ArrayBuffer, (nuint)(vertices.Length * sizeof(Vertex)), p, BufferUsageARB.StaticDraw);
            }
            _vertexCount = vertices.Length;
            _isDirty = false;
        }

        public void Draw()
        {
            if (_vertexCount == 0) return;

            _gl.BindVertexArray(_vao);
            _gl.DrawArrays(PrimitiveType.Triangles, 0, (uint)_vertexCount);
        }

        public void Dispose()
        {
            _gl.DeleteVertexArray(_vao);
            _gl.DeleteBuffer(_vbo);
        }
    }
}
