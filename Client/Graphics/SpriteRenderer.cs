using Silk.NET.OpenGL;
using System.IO;
using System.Numerics;
using Robust.Shared.Maths;

namespace Client.Graphics
{
    public class SpriteRenderer : IDisposable
    {
        private readonly GL _gl;
        private readonly uint _shaderProgram;
        private readonly uint _vao;
        private readonly uint _vbo;

        private readonly int _uProjectionLocation;
        private readonly int _uViewLocation;
        private readonly int _uModelLocation;

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
            _uModelLocation = _gl.GetUniformLocation(_shaderProgram, "uModel");

            _vao = _gl.GenVertexArray();
            _vbo = _gl.GenBuffer();

            _gl.BindVertexArray(_vao);
            _gl.BindBuffer(BufferTargetARB.ArrayBuffer, _vbo);

            unsafe
            {
                _gl.BufferData(BufferTargetARB.ArrayBuffer, (nuint)(6 * 4 * sizeof(float)), null, BufferUsageARB.DynamicDraw);
            }

            unsafe
            {
                _gl.EnableVertexAttribArray(0);
                _gl.VertexAttribPointer(0, 2, VertexAttribPointerType.Float, false, 4 * sizeof(float), (void*)0);

                _gl.EnableVertexAttribArray(1);
                _gl.VertexAttribPointer(1, 2, VertexAttribPointerType.Float, false, 4 * sizeof(float), (void*)(2 * sizeof(float)));
            }

            _gl.BindBuffer(BufferTargetARB.ArrayBuffer, 0);
            _gl.BindVertexArray(0);
        }

        public unsafe void Begin(Matrix4x4 view, Matrix4x4 projection)
        {
            _gl.UseProgram(_shaderProgram);
            _gl.UniformMatrix4(_uViewLocation, 1, false, (float*)&view);
            _gl.UniformMatrix4(_uProjectionLocation, 1, false, (float*)&projection);
        }

        public void End()
        {
        }

        public unsafe void Draw(uint textureId, Box2 uv, Vector2 position, Vector2 size, Color color)
        {
            _gl.BindTexture(TextureTarget.Texture2D, textureId);
            _gl.BindVertexArray(_vao);
            _gl.BindBuffer(BufferTargetARB.ArrayBuffer, _vbo);

            Matrix4x4 model = Matrix4x4.CreateScale(size.X, size.Y, 1.0f) * Matrix4x4.CreateTranslation(position.X, position.Y, 0.0f);
            _gl.UniformMatrix4(_uModelLocation, 1, false, (float*)&model);

            float[] vertices = new float[] {
                //X  Y      U    V
                0, 1, uv.Left, uv.Top,
                1, 0, uv.Right, uv.Bottom,
                0, 0, uv.Left, uv.Bottom,
                1, 1, uv.Right, uv.Top,
                1, 0, uv.Right, uv.Bottom,
                0, 1, uv.Left, uv.Top,
            };

            fixed (float* p = vertices)
            {
                _gl.BufferSubData(BufferTargetARB.ArrayBuffer, 0, (nuint)(vertices.Length * sizeof(float)), p);
            }

            _gl.DrawArrays(PrimitiveType.Triangles, 0, 6);
            _gl.BindVertexArray(0);
        }

        public unsafe void DrawQuad(Vector2 position, Vector2 size, Color color)
        {
            _gl.BindTexture(TextureTarget.Texture2D, 0); // Unbind texture
            _gl.BindVertexArray(_vao);
            _gl.BindBuffer(BufferTargetARB.ArrayBuffer, _vbo);

            Matrix4x4 model = Matrix4x4.CreateScale(size.X, size.Y, 1.0f) * Matrix4x4.CreateTranslation(position.X, position.Y, 0.0f);
            _gl.UniformMatrix4(_uModelLocation, 1, false, (float*)&model);

            float[] vertices = new float[] {
                 0, 1, 0, 1,
                 1, 0, 1, 0,
                 0, 0, 0, 0,
                 1, 1, 1, 1,
                 1, 0, 1, 0,
                 0, 1, 0, 1,
            };

            fixed (float* p = vertices)
            {
                _gl.BufferSubData(BufferTargetARB.ArrayBuffer, 0, (nuint)(vertices.Length * sizeof(float)), p);
            }

            _gl.DrawArrays(PrimitiveType.Triangles, 0, 6);
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
            _gl.DeleteProgram(_shaderProgram);
        }
    }
}
