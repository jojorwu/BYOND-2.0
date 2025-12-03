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

    public class SpriteRenderer : IDisposable
    {
        private const int MaxQuads = 2000;
        private const int MaxVertices = MaxQuads * 4;
        private const int MaxIndices = MaxQuads * 6;

        private readonly GL _gl;
        private readonly uint _shaderProgram;
        private readonly uint _vao;
        private readonly uint _vbo;
        private readonly uint _ebo;

        private readonly int _uProjectionLocation;
        private readonly int _uViewLocation;

        private readonly List<Vertex> _vertices = new(MaxVertices);
        private uint _activeTextureId;

        public SpriteRenderer(GL gl)
        {
            _gl = gl;

            string vertexShaderSource = File.ReadAllText("Client/Shaders/sprite.vert");
            string fragmentShaderSource = File.ReadAllText("Client/Shaders/sprite.frag");

            uint vertexShader = CompileShader(ShaderType.VertexShader, vertexShaderSource);
            uint fragmentShader = CompileShader(ShaderType.FragmentShader, fragmentShaderSource);

            _shaderProgram = _gl.CreateProgram();
            _gl.AttachShader(_shaderProgram, vertexShader);
            _gl.AttachShader(_shaderProgram, fragmentShader);
            _gl.LinkProgram(_shaderProgram);
            _gl.GetProgram(_shaderProgram, GLEnum.LinkStatus, out int success);
            if (success == 0)
            {
                string infoLog = _gl.GetProgramInfoLog(_shaderProgram);
                throw new Exception($"Error linking shader program: {infoLog}");
            }

            _gl.DetachShader(_shaderProgram, vertexShader);
            _gl.DetachShader(_shaderProgram, fragmentShader);
            _gl.DeleteShader(vertexShader);
            _gl.DeleteShader(fragmentShader);

            _uProjectionLocation = _gl.GetUniformLocation(_shaderProgram, "uProjection");
            _uViewLocation = _gl.GetUniformLocation(_shaderProgram, "uView");

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
                 var size = sizeof(Vertex);
                _gl.EnableVertexAttribArray(0); // Position
                _gl.VertexAttribPointer(0, 2, VertexAttribPointerType.Float, false, (uint)size, (void*)0);

                _gl.EnableVertexAttribArray(1); // TexCoords
                _gl.VertexAttribPointer(1, 2, VertexAttribPointerType.Float, false, (uint)size, (void*)Marshal.OffsetOf<Vertex>("TexCoords"));

                _gl.EnableVertexAttribArray(2); // Color
                _gl.VertexAttribPointer(2, 4, VertexAttribPointerType.Float, false, (uint)size, (void*)Marshal.OffsetOf<Vertex>("Color"));
            }

            _gl.BindBuffer(BufferTargetARB.ArrayBuffer, 0);
            _gl.BindVertexArray(0);
        }

        public unsafe void Begin(Matrix4x4 view, Matrix4x4 projection)
        {
            _gl.UseProgram(_shaderProgram);
            _gl.UniformMatrix4(_uViewLocation, 1, false, (float*)&view);
            _gl.UniformMatrix4(_uProjectionLocation, 1, false, (float*)&projection);

            _activeTextureId = 0;
            _vertices.Clear();
        }

        public void End()
        {
            Flush();
        }

        private void Flush()
        {
            if (_vertices.Count == 0)
                return;

            _gl.BindTexture(TextureTarget.Texture2D, _activeTextureId);
            _gl.BindVertexArray(_vao);
            _gl.BindBuffer(BufferTargetARB.ArrayBuffer, _vbo);

            unsafe
            {
                fixed (Vertex* p = &_vertices[0])
                {
                    _gl.BufferSubData(BufferTargetARB.ArrayBuffer, 0, (nuint)(_vertices.Count * sizeof(Vertex)), p);
                }
            }

            _gl.DrawElements(PrimitiveType.Triangles, (uint)(_vertices.Count / 4 * 6), DrawElementsType.UnsignedInt, null);

            _vertices.Clear();
        }

        public void Draw(uint textureId, Box2 uv, Vector2 position, Vector2 size, Color color)
        {
            if (textureId != _activeTextureId)
            {
                if(_activeTextureId != 0)
                    Flush();
                _activeTextureId = textureId;
            }

            if (_vertices.Count + 4 > MaxVertices)
            {
                Flush();
            }

            _vertices.Add(new Vertex(position, new Vector2(uv.Left, uv.Top), color));
            _vertices.Add(new Vertex(position + new Vector2(size.X, 0), new Vector2(uv.Right, uv.Top), color));
            _vertices.Add(new Vertex(position + size, new Vector2(uv.Right, uv.Bottom), color));
            _vertices.Add(new Vertex(position + new Vector2(0, size.Y), new Vector2(uv.Left, uv.Bottom), color));
        }

        public void DrawQuad(Vector2 position, Vector2 size, Color color)
        {
            Draw(0, new Box2(0, 0, 1, 1), position, size, color);
        }

        private uint CompileShader(ShaderType type, string source)
        {
            uint shader = _gl.CreateShader(type);
            _gl.ShaderSource(shader, source);
            _gl.CompileShader(shader);
            _gl.GetShader(shader, ShaderParameterName.CompileStatus, out int success);
            if (success == 0)
            {
                string infoLog = _gl.GetShaderInfoLog(shader);
                throw new Exception($"Error compiling shader of type {type}: {infoLog}");
            }
            return shader;
        }

        public void Dispose()
        {
            _gl.DeleteVertexArray(_vao);
            _gl.DeleteBuffer(_vbo);
            _gl.DeleteBuffer(_ebo);
            _gl.DeleteProgram(_shaderProgram);
        }
    }
}
