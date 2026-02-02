using Shared.Models;
using Shared.Interfaces;
using Shared.Enums;
using Shared.Operations;
using Shared.Services;
using Silk.NET.OpenGL;
using System;
using System.Numerics;
using System.Runtime.InteropServices;

namespace Client.Graphics
{
    [StructLayout(LayoutKind.Sequential)]
    public struct MeshVertex
    {
        public Vector3 Position;
        public Vector3 Normal;
        public Vector2 TexCoords;

        public MeshVertex(Vector3 position, Vector3 normal, Vector2 texCoords)
        {
            Position = position;
            Normal = normal;
            TexCoords = texCoords;
        }
    }

    public class Mesh : IDisposable
    {
        private readonly GL _gl;
        private readonly uint _vao;
        private readonly uint _vbo;
        private readonly uint _ebo;
        private readonly uint _indexCount;

        public Mesh(GL gl, MeshVertex[] vertices, uint[] indices)
        {
            _gl = gl;
            _indexCount = (uint)indices.Length;

            _vao = _gl.GenVertexArray();
            _vbo = _gl.GenBuffer();
            _ebo = _gl.GenBuffer();

            _gl.BindVertexArray(_vao);

            _gl.BindBuffer(BufferTargetARB.ArrayBuffer, _vbo);
            unsafe
            {
                fixed (MeshVertex* v = &vertices[0])
                {
                    _gl.BufferData(BufferTargetARB.ArrayBuffer, (nuint)(vertices.Length * sizeof(MeshVertex)), v, BufferUsageARB.StaticDraw);
                }
            }

            _gl.BindBuffer(BufferTargetARB.ElementArrayBuffer, _ebo);
            unsafe
            {
                fixed (uint* i = &indices[0])
                {
                    _gl.BufferData(BufferTargetARB.ElementArrayBuffer, (nuint)(indices.Length * sizeof(uint)), i, BufferUsageARB.StaticDraw);
                }
            }

            unsafe
            {
                var size = (uint)sizeof(MeshVertex);
                _gl.EnableVertexAttribArray(0); // Position
                _gl.VertexAttribPointer(0, 3, VertexAttribPointerType.Float, false, size, (void*)0);

                _gl.EnableVertexAttribArray(1); // Normal
                _gl.VertexAttribPointer(1, 3, VertexAttribPointerType.Float, false, size, (void*)(sizeof(Vector3)));

                _gl.EnableVertexAttribArray(2); // TexCoords
                _gl.VertexAttribPointer(2, 2, VertexAttribPointerType.Float, false, size, (void*)(sizeof(Vector3) * 2));
            }

            _gl.BindVertexArray(0);
        }

        public void Draw()
        {
            _gl.BindVertexArray(_vao);
            unsafe
            {
                _gl.DrawElements(PrimitiveType.Triangles, _indexCount, DrawElementsType.UnsignedInt, null);
            }
        }

        public void Dispose()
        {
            _gl.DeleteVertexArray(_vao);
            _gl.DeleteBuffer(_vbo);
            _gl.DeleteBuffer(_ebo);
        }
    }
}
