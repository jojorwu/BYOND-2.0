using Silk.NET.OpenGL;
using Silk.NET.Maths;
using System.IO;
using System.Numerics;

namespace Editor
{
    public class SpriteRenderer : IDisposable
    {
        private readonly GL _gl;
        private readonly uint _shaderProgram;
        private readonly uint _vao;
        private readonly uint _vbo;

        private readonly int _uProjectionLocation;
        private readonly int _uModelLocation;

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

            _uProjectionLocation = _gl.GetUniformLocation(_shaderProgram, "uProjection");
            _uModelLocation = _gl.GetUniformLocation(_shaderProgram, "uModel");

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

            _gl.BindVertexArray(_vao);
            _gl.BindBuffer(BufferTargetARB.ArrayBuffer, _vbo);
            unsafe
            {
                fixed (float* buf = vertices)
                {
                    _gl.BufferData(BufferTargetARB.ArrayBuffer, (nuint)(vertices.Length * sizeof(float)), buf, BufferUsageARB.StaticDraw);
                }
            }

            unsafe
            {
                _gl.VertexAttribPointer(0, 2, VertexAttribPointerType.Float, false, 4 * sizeof(float), (void*)0);
                _gl.EnableVertexAttribArray(0);

                _gl.VertexAttribPointer(1, 2, VertexAttribPointerType.Float, false, 4 * sizeof(float), (void*)(2 * sizeof(float)));
                _gl.EnableVertexAttribArray(1);
            }

            _gl.BindBuffer(BufferTargetARB.ArrayBuffer, 0);
            _gl.BindVertexArray(0);
        }

        public unsafe void Draw(uint textureId, Vector2D<int> position, Vector2D<int> size, float rotate, Matrix4x4 projection)
        {
            _gl.UseProgram(_shaderProgram);

            Matrix4x4 model = Matrix4x4.Identity;
            model *= Matrix4x4.CreateTranslation(new Vector3(position.X, position.Y, 0.0f));

            model *= Matrix4x4.CreateTranslation(new Vector3(0.5f * size.X, 0.5f * size.Y, 0.0f));
            model *= Matrix4x4.CreateRotationZ(rotate);
            model *= Matrix4x4.CreateTranslation(new Vector3(-0.5f * size.X, -0.5f * size.Y, 0.0f));

            model *= Matrix4x4.CreateScale(new Vector3(size.X, size.Y, 1.0f));

            _gl.UniformMatrix4(_uProjectionLocation, 1, false, in projection.M11);
            _gl.UniformMatrix4(_uModelLocation, 1, false, in model.M11);

            _gl.ActiveTexture(TextureUnit.Texture0);
            _gl.BindTexture(TextureTarget.Texture2D, textureId);

            _gl.BindVertexArray(_vao);
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
