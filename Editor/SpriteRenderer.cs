using Silk.NET.OpenGL;
using System.IO;
using System.Runtime.InteropServices;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;


namespace Editor
{
    public struct SpriteInstance
    {
        public Matrix4x4 Model;
        public uint TextureId;
    }

    public class SpriteRenderer : IDisposable
    {
        private readonly GL _gl;
        private readonly uint _shaderProgram;
        private readonly uint _vao;
        private readonly uint _vbo;
        private readonly uint _instanceVbo;

        private readonly int _uProjectionLocation;

        private const int MaxSprites = 10000;

        public unsafe SpriteRenderer(GL gl)
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

            _uProjectionLocation = _gl.GetUniformLocation(_shaderProgram, "uProjection");

            float[] vertices =
            {
                0.0f, 1.0f, 0.0f, 1.0f,
                1.0f, 0.0f, 1.0f, 0.0f,
                0.0f, 0.0f, 0.0f, 0.0f,
                0.0f, 1.0f, 0.0f, 1.0f,
                1.0f, 1.0f, 1.0f, 1.0f,
                1.0f, 0.0f, 1.0f, 0.0f
            };

            _vao = _gl.GenVertexArray();
            _vbo = _gl.GenBuffer();
            _instanceVbo = _gl.GenBuffer();

            _gl.BindVertexArray(_vao);

            _gl.BindBuffer(BufferTargetARB.ArrayBuffer, _vbo);
            fixed (void* v = vertices)
            {
                _gl.BufferData(BufferTargetARB.ArrayBuffer, (uint)(vertices.Length * sizeof(float)), v, BufferUsageARB.StaticDraw);
            }

            _gl.VertexAttribPointer(0, 2, VertexAttribPointerType.Float, false, 4 * sizeof(float), (void*)0);
            _gl.EnableVertexAttribArray(0);
            _gl.VertexAttribPointer(1, 2, VertexAttribPointerType.Float, false, 4 * sizeof(float), (void*)(2 * sizeof(float)));
            _gl.EnableVertexAttribArray(1);

            _gl.BindBuffer(BufferTargetARB.ArrayBuffer, _instanceVbo);
            _gl.BufferData(BufferTargetARB.ArrayBuffer, (uint)(MaxSprites * Marshal.SizeOf<Matrix4x4>()), IntPtr.Zero, BufferUsageARB.DynamicDraw);

            int modelMatrixLocation = _gl.GetAttribLocation(_shaderProgram, "aModel");
            for (int i = 0; i < 4; i++)
            {
                _gl.EnableVertexAttribArray((uint)(modelMatrixLocation + i));
                _gl.VertexAttribPointer((uint)(modelMatrixLocation + i), 4, VertexAttribPointerType.Float, false, (uint)Marshal.SizeOf<Matrix4x4>(), (void*)(i * Marshal.SizeOf<Vector4>()));
                _gl.VertexAttribDivisor((uint)(modelMatrixLocation + i), 1);
            }

            _gl.BindBuffer(BufferTargetARB.ArrayBuffer, 0);
            _gl.BindVertexArray(0);
        }

        public unsafe void Draw(List<SpriteInstance> sprites, Matrix4x4 projection)
        {
            if (sprites.Count == 0) return;

            _gl.UseProgram(_shaderProgram);
            _gl.UniformMatrix4(_uProjectionLocation, 1, false, (float*)&projection);

            _gl.BindBuffer(BufferTargetARB.ArrayBuffer, _instanceVbo);
            var instances = sprites.Select(s => s.Model).ToArray();
            fixed (void* i = instances)
            {
                _gl.BufferSubData(BufferTargetARB.ArrayBuffer, 0, (uint)(instances.Length * Marshal.SizeOf<Matrix4x4>()), i);
            }

            _gl.BindVertexArray(_vao);

            uint currentTextureId = 0;
            uint instanceOffset = 0;
            uint instanceCount = 0;

            for (int i = 0; i < sprites.Count; i++)
            {
                var sprite = sprites[i];
                if (currentTextureId == 0)
                {
                    currentTextureId = sprite.TextureId;
                }

                if (sprite.TextureId != currentTextureId)
                {
                    _gl.ActiveTexture(TextureUnit.Texture0);
                    _gl.BindTexture(TextureTarget.Texture2D, currentTextureId);
                    _gl.DrawArraysInstancedBaseInstance((GLEnum)PrimitiveType.Triangles, 0, 6, instanceCount, instanceOffset);

                    currentTextureId = sprite.TextureId;
                    instanceOffset = (uint)i;
                    instanceCount = 0;
                }
                instanceCount++;
            }

            if (instanceCount > 0)
            {
                _gl.ActiveTexture(TextureUnit.Texture0);
                _gl.BindTexture(TextureTarget.Texture2D, currentTextureId);
                _gl.DrawArraysInstancedBaseInstance((GLEnum)PrimitiveType.Triangles, 0, 6, instanceCount, instanceOffset);
            }

            _gl.BindVertexArray(0);
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
            _gl.DeleteBuffer(_instanceVbo);
            _gl.DeleteProgram(_shaderProgram);
        }
    }
}
