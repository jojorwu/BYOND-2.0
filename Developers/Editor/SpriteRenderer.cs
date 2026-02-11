using Silk.NET.OpenGL;
using System;
using System.Collections.Generic;
using System.IO;
using System.Numerics;
using System.Runtime.InteropServices;
using Robust.Shared.Maths;

namespace Editor
{
    [StructLayout(LayoutKind.Sequential)]
    public struct EditorVertex
    {
        public Vector2 Position;
        public Vector2 TexCoord;
        public Vector4 Color;

        public EditorVertex(Vector2 position, Vector2 texCoord, Vector4 color)
        {
            Position = position;
            TexCoord = texCoord;
            Color = color;
        }
    }

    public class SpriteRenderer : IDisposable
    {
        private const int MaxQuads = 8192;
        private const int MaxVertices = MaxQuads * 4;
        private const int MaxIndices = MaxQuads * 6;

        private readonly GL _gl;
        private readonly uint _shaderProgram;
        private readonly uint _vao;
        private readonly uint _vbo;
        private readonly uint _ebo;

        private readonly EditorVertex[] _vertices = new EditorVertex[MaxVertices];
        private int _vertexCount = 0;
        private uint _currentTextureId = 0;
        private Matrix4x4 _projectionMatrix;

        public SpriteRenderer(GL gl)
        {
            _gl = gl;

            string vertexShaderSource = File.ReadAllText("Editor/Shaders/sprite.vert");
            string fragmentShaderSource = File.ReadAllText("Editor/Shaders/sprite.frag");

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

            _vao = _gl.GenVertexArray();
            _vbo = _gl.GenBuffer();
            _ebo = _gl.GenBuffer();

            _gl.BindVertexArray(_vao);

            _gl.BindBuffer(BufferTargetARB.ArrayBuffer, _vbo);
            unsafe {
                _gl.BufferData(BufferTargetARB.ArrayBuffer, (nuint)(MaxVertices * sizeof(EditorVertex)), null, BufferUsageARB.DynamicDraw);
            }

            _gl.BindBuffer(BufferTargetARB.ElementArrayBuffer, _ebo);
            var indices = new uint[MaxIndices];
            for (uint i = 0, v = 0; i < MaxIndices; i += 6, v += 4)
            {
                indices[i + 0] = v + 0;
                indices[i + 1] = v + 1;
                indices[i + 2] = v + 2;
                indices[i + 3] = v + 2;
                indices[i + 4] = v + 3;
                indices[i + 5] = v + 0;
            }
            unsafe {
                fixed (uint* ptr = indices)
                    _gl.BufferData(BufferTargetARB.ElementArrayBuffer, (nuint)(MaxIndices * sizeof(uint)), ptr, BufferUsageARB.StaticDraw);
            }

            unsafe {
                var size = (uint)sizeof(EditorVertex);
                _gl.EnableVertexAttribArray(0);
                _gl.VertexAttribPointer(0, 2, VertexAttribPointerType.Float, false, size, (void*)0);
                _gl.EnableVertexAttribArray(1);
                _gl.VertexAttribPointer(1, 2, VertexAttribPointerType.Float, false, size, (void*)(2 * sizeof(float)));
                _gl.EnableVertexAttribArray(2);
                _gl.VertexAttribPointer(2, 4, VertexAttribPointerType.Float, false, size, (void*)(4 * sizeof(float)));
            }

            _gl.BindVertexArray(0);
        }

        public void Begin(Matrix4x4 projection)
        {
            _projectionMatrix = projection;
            _vertexCount = 0;
            _currentTextureId = 0;
        }

        public void Draw(uint textureId, Vector2 position, Vector2 size, Vector4 color, Box2 uv)
        {
            if (textureId == 0) return;

            if (textureId != _currentTextureId || _vertexCount + 4 > MaxVertices)
            {
                Flush();
                _currentTextureId = textureId;
            }

            _vertices[_vertexCount++] = new EditorVertex(position, new Vector2(uv.Left, uv.Top), color);
            _vertices[_vertexCount++] = new EditorVertex(position + new Vector2(size.X, 0), new Vector2(uv.Right, uv.Top), color);
            _vertices[_vertexCount++] = new EditorVertex(position + size, new Vector2(uv.Right, uv.Bottom), color);
            _vertices[_vertexCount++] = new EditorVertex(position + new Vector2(0, size.Y), new Vector2(uv.Left, uv.Bottom), color);
        }

        public unsafe void Flush()
        {
            if (_vertexCount == 0 || _currentTextureId == 0) return;

            _gl.UseProgram(_shaderProgram);
            int uProj = _gl.GetUniformLocation(_shaderProgram, "uProjection");
            _gl.UniformMatrix4(uProj, 1, false, in _projectionMatrix.M11);

            int uModel = _gl.GetUniformLocation(_shaderProgram, "uModel");
            var identity = Matrix4x4.Identity;
            _gl.UniformMatrix4(uModel, 1, false, in identity.M11);

            _gl.ActiveTexture(TextureUnit.Texture0);
            _gl.BindTexture(TextureTarget.Texture2D, _currentTextureId);

            _gl.BindVertexArray(_vao);
            _gl.BindBuffer(BufferTargetARB.ArrayBuffer, _vbo);
            fixed (EditorVertex* ptr = _vertices)
            {
                _gl.BufferSubData(BufferTargetARB.ArrayBuffer, 0, (nuint)(_vertexCount * sizeof(EditorVertex)), ptr);
            }

            _gl.DrawElements(PrimitiveType.Triangles, (uint)(_vertexCount / 4 * 6), DrawElementsType.UnsignedInt, null);

            _vertexCount = 0;
        }

        public void End()
        {
            Flush();
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
