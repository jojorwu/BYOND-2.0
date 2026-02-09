using Silk.NET.OpenGL;
using System;
using System.Collections.Generic;
using System.IO;
using System.Numerics;
using System.Runtime.InteropServices;
using System.Threading;
using Robust.Shared.Maths;

namespace Client.Graphics
{
    [StructLayout(LayoutKind.Sequential)]
    public struct Vertex
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
        public int Plane;
        public Box2? Scissor;
        public float Rotation;

        // Packed key for sorting
        public long SortKey => ((long)Plane << 48) | ((long)(Layer * 1000) << 16) | (TextureId & 0xFFFF);
    }

    public class SpriteRenderer : IDisposable
    {
        private const int MaxQuads = 16384;
        private const int MaxVertices = MaxQuads * 4;
        private const int MaxIndices = MaxQuads * 6;

        private readonly GL _gl;
        private readonly Shader _shader;
        private readonly uint _vao;
        private readonly uint _vbo;
        private readonly uint _ebo;

        private readonly Vertex[] _vertices = new Vertex[MaxVertices];
        private int _vertexCount = 0;

        private readonly ThreadLocal<List<SpriteDrawCommand>> _threadLocalCommands = new(() => new List<SpriteDrawCommand>(), true);
        private readonly List<SpriteDrawCommand> _mergedCommands = new();

        private uint _activeTextureId;
        private Box2? _activeScissor;

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

            foreach (var list in _threadLocalCommands.Values)
            {
                list.Clear();
            }
        }

        public void Draw(uint textureId, Box2 uv, Vector2 position, Vector2 size, Color color, float layer = 0, int plane = 0, Box2? scissor = null, float rotation = 0)
        {
            _threadLocalCommands.Value!.Add(new SpriteDrawCommand
            {
                TextureId = textureId,
                Uv = uv,
                Position = position,
                Size = size,
                Color = color,
                Layer = layer,
                Plane = plane,
                Scissor = scissor,
                Rotation = rotation
            });
        }

        public void DrawQuad(Vector2 position, Vector2 size, Color color, float layer = 0, int plane = 0)
        {
            Draw(0, new Box2(0, 0, 1, 1), position, size, color, layer, plane);
        }

        public void End()
        {
            _mergedCommands.Clear();
            foreach (var list in _threadLocalCommands.Values)
            {
                _mergedCommands.AddRange(list);
            }

            if (_mergedCommands.Count == 0) return;

            // Optimized sort using packed key
            _mergedCommands.Sort((a, b) => a.SortKey.CompareTo(b.SortKey));

            _activeTextureId = _mergedCommands[0].TextureId;
            _activeScissor = _mergedCommands[0].Scissor;
            _vertexCount = 0;

            foreach (var cmd in _mergedCommands)
            {
                if (cmd.TextureId != _activeTextureId || cmd.Scissor != _activeScissor || _vertexCount + 4 > MaxVertices)
                {
                    Flush();
                    _activeTextureId = cmd.TextureId;
                    _activeScissor = cmd.Scissor;
                }

                if (cmd.Rotation == 0)
                {
                    _vertices[_vertexCount++] = new Vertex(cmd.Position, new Vector2(cmd.Uv.Left, cmd.Uv.Top), cmd.Color);
                    _vertices[_vertexCount++] = new Vertex(cmd.Position + new Vector2(cmd.Size.X, 0), new Vector2(cmd.Uv.Right, cmd.Uv.Top), cmd.Color);
                    _vertices[_vertexCount++] = new Vertex(cmd.Position + cmd.Size, new Vector2(cmd.Uv.Right, cmd.Uv.Bottom), cmd.Color);
                    _vertices[_vertexCount++] = new Vertex(cmd.Position + new Vector2(0, cmd.Size.Y), new Vector2(cmd.Uv.Left, cmd.Uv.Bottom), cmd.Color);
                }
                else
                {
                    var cos = (float)Math.Cos(cmd.Rotation);
                    var sin = (float)Math.Sin(cmd.Rotation);
                    var center = cmd.Position + cmd.Size * 0.5f;

                    Vector2 Rotate(Vector2 p)
                    {
                        var rel = p - center;
                        return new Vector2(
                            rel.X * cos - rel.Y * sin + center.X,
                            rel.X * sin + rel.Y * cos + center.Y
                        );
                    }

                    _vertices[_vertexCount++] = new Vertex(Rotate(cmd.Position), new Vector2(cmd.Uv.Left, cmd.Uv.Top), cmd.Color);
                    _vertices[_vertexCount++] = new Vertex(Rotate(cmd.Position + new Vector2(cmd.Size.X, 0)), new Vector2(cmd.Uv.Right, cmd.Uv.Top), cmd.Color);
                    _vertices[_vertexCount++] = new Vertex(Rotate(cmd.Position + cmd.Size), new Vector2(cmd.Uv.Right, cmd.Uv.Bottom), cmd.Color);
                    _vertices[_vertexCount++] = new Vertex(Rotate(cmd.Position + new Vector2(0, cmd.Size.Y)), new Vector2(cmd.Uv.Left, cmd.Uv.Bottom), cmd.Color);
                }
            }

            Flush();
        }

        private unsafe void Flush()
        {
            if (_vertexCount == 0)
                return;

            if (_activeScissor.HasValue)
            {
                _gl.Enable(EnableCap.ScissorTest);
                var s = _activeScissor.Value;
                _gl.Scissor((int)s.Left, (int)s.Bottom, (uint)s.Width, (uint)s.Height);
            }
            else
            {
                _gl.Disable(EnableCap.ScissorTest);
            }

            _gl.ActiveTexture(TextureUnit.Texture0);
            _gl.BindTexture(TextureTarget.Texture2D, _activeTextureId);
            _shader.SetUniform("uTexture", 0);

            _gl.BindVertexArray(_vao);
            _gl.BindBuffer(BufferTargetARB.ArrayBuffer, _vbo);

            // Buffer orphaning to avoid sync stalls
            _gl.BufferData(BufferTargetARB.ArrayBuffer, (nuint)(MaxVertices * sizeof(Vertex)), null, BufferUsageARB.DynamicDraw);

            fixed (Vertex* p = &_vertices[0])
            {
                _gl.BufferSubData(BufferTargetARB.ArrayBuffer, 0, (nuint)(_vertexCount * sizeof(Vertex)), p);
            }

            _gl.DrawElements(PrimitiveType.Triangles, (uint)(_vertexCount / 4 * 6), DrawElementsType.UnsignedInt, null);

            _vertexCount = 0;
        }

        public void Dispose()
        {
            _threadLocalCommands.Dispose();
            _gl.DeleteVertexArray(_vao);
            _gl.DeleteBuffer(_vbo);
            _gl.DeleteBuffer(_ebo);
            _shader.Dispose();
        }
    }
}
