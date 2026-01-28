using Silk.NET.OpenGL;
using System;
using System.Collections.Generic;
using System.IO;
using System.Numerics;
using System.Runtime.InteropServices;
using Robust.Shared.Maths;

namespace Client.Graphics
{
    [StructLayout(LayoutKind.Sequential)]
    struct Vertex
    {
        public Vector2 Position;
        public Vector2 TexCoords;
        public Color Color;

        public Vertex(Vector2 position, Vector2 texCoords, Color color)
        {
            Position = position;
            TexCoords = texCoords;
            Color = color;
        }
    }

    public struct SpriteDrawCommand
    {
        public uint TextureId;
        public Box2 Uv;
        public Vector2 Position;
        public Vector2 Size;
        public Color Color;
        public float Layer;
    }

    public class SpriteRenderer : IDisposable
    {
        private const int MaxQuads = 2000;
        private const int MaxVertices = MaxQuads * 4;
        private const int MaxIndices = MaxQuads * 6;

        private readonly GL _gl;
        private readonly Shader _shader;
        private readonly uint _vao;
        private readonly uint _vbo;
        private readonly uint _ebo;

        private readonly List<Vertex> _vertices = new(MaxVertices);
        private readonly List<SpriteDrawCommand> _commands = new();
        private uint _activeTextureId;

        public SpriteRenderer(GL gl)
        {
            _gl = gl;

            _shader = new Shader(_gl, File.ReadAllText("Shaders/sprite.vert"), File.ReadAllText("Shaders/sprite.frag"));

            _vao = _gl.GenVertexArray();
            _vbo = _gl.GenBuffer();
            _ebo = _gl.GenBuffer();

            _gl.BindVertexArray(_vao);

            _gl.BindBuffer(BufferTargetARB.ArrayBuffer, _vbo);
            unsafe
            {
                _gl.BufferData(BufferTargetARB.ArrayBuffer, (nuint)(MaxVertices * sizeof(Vertex)), null, BufferUsageARB.DynamicDraw);
            }

            _gl.BindBuffer(BufferTargetARB.ElementArrayBuffer, _ebo);

            var quadIndices = new uint[MaxIndices];
            uint offset = 0;
            for (int i = 0; i < MaxIndices; i += 6)
            {
                quadIndices[i + 0] = offset + 0;
                quadIndices[i + 1] = offset + 1;
                quadIndices[i + 2] = offset + 2;

                quadIndices[i + 3] = offset + 2;
                quadIndices[i + 4] = offset + 3;
                quadIndices[i + 5] = offset + 0;

                offset += 4;
            }

            unsafe
            {
                fixed(uint* i = &quadIndices[0])
                    _gl.BufferData(BufferTargetARB.ElementArrayBuffer, (nuint)(MaxIndices * sizeof(uint)), i, BufferUsageARB.StaticDraw);
            }

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

            _gl.BindBuffer(BufferTargetARB.ArrayBuffer, 0);
            _gl.BindVertexArray(0);
        }

        public unsafe void Begin(Matrix4x4 view, Matrix4x4 projection)
        {
            _shader.Use();
            _shader.SetUniform("uView", view);
            _shader.SetUniform("uProjection", projection);

            _commands.Clear();
        }

        public void Draw(uint textureId, Box2 uv, Vector2 position, Vector2 size, Color color, float layer = 0)
        {
            _commands.Add(new SpriteDrawCommand
            {
                TextureId = textureId,
                Uv = uv,
                Position = position,
                Size = size,
                Color = color,
                Layer = layer
            });
        }

        public void DrawQuad(Vector2 position, Vector2 size, Color color, float layer = 0)
        {
            Draw(0, new Box2(0, 0, 1, 1), position, size, color, layer);
        }

        public void End()
        {
            if (_commands.Count == 0) return;

            // Sort by Layer, then by TextureId to minimize switches
            _commands.Sort((a, b) =>
            {
                int layerCmp = a.Layer.CompareTo(b.Layer);
                if (layerCmp != 0) return layerCmp;
                return a.TextureId.CompareTo(b.TextureId);
            });

            _activeTextureId = _commands[0].TextureId;
            _vertices.Clear();

            foreach (var cmd in _commands)
            {
                if (cmd.TextureId != _activeTextureId || _vertices.Count + 4 > MaxVertices)
                {
                    Flush();
                    _activeTextureId = cmd.TextureId;
                }

                _vertices.Add(new Vertex(cmd.Position, new Vector2(cmd.Uv.Left, cmd.Uv.Top), cmd.Color));
                _vertices.Add(new Vertex(cmd.Position + new Vector2(cmd.Size.X, 0), new Vector2(cmd.Uv.Right, cmd.Uv.Top), cmd.Color));
                _vertices.Add(new Vertex(cmd.Position + cmd.Size, new Vector2(cmd.Uv.Right, cmd.Uv.Bottom), cmd.Color));
                _vertices.Add(new Vertex(cmd.Position + new Vector2(0, cmd.Size.Y), new Vector2(cmd.Uv.Left, cmd.Uv.Bottom), cmd.Color));
            }

            Flush();
        }

        private unsafe void Flush()
        {
            if (_vertices.Count == 0)
                return;

            _gl.ActiveTexture(TextureUnit.Texture0);
            _gl.BindTexture(TextureTarget.Texture2D, _activeTextureId);
            _shader.SetUniform("uTexture", 0);

            _gl.BindVertexArray(_vao);
            _gl.BindBuffer(BufferTargetARB.ArrayBuffer, _vbo);

            fixed (Vertex* p = CollectionsMarshal.AsSpan(_vertices))
            {
                _gl.BufferSubData(BufferTargetARB.ArrayBuffer, 0, (nuint)(_vertices.Count * sizeof(Vertex)), p);
            }

            _gl.DrawElements(PrimitiveType.Triangles, (uint)(_vertices.Count / 4 * 6), DrawElementsType.UnsignedInt, null);

            _vertices.Clear();
        }

        public void Dispose()
        {
            _gl.DeleteVertexArray(_vao);
            _gl.DeleteBuffer(_vbo);
            _gl.DeleteBuffer(_ebo);
            _shader.Dispose();
        }
    }
}
