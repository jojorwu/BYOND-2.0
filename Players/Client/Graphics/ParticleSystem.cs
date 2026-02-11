using System;
using Silk.NET.OpenGL;
using System.Numerics;
using System.Runtime.InteropServices;
using Robust.Shared.Maths;

namespace Client.Graphics
{
    public class ParticleSystem : IDisposable
    {
        private readonly GL _gl;
        private readonly Shader _shader;
        private readonly uint _vao;
        private readonly uint _vbo;

        [StructLayout(LayoutKind.Sequential)]
        public struct Particle
        {
            public Vector2 Position;
            public Vector2 Velocity;
            public Vector4 Color;
            public float Life;
            public float MaxLife;
        }

        private const int MaxParticles = 10000;
        private readonly Particle[] _particles = new Particle[MaxParticles];
        private int _activeCount = 0;
        private readonly Random _random = new();

        public ParticleSystem(GL gl)
        {
            _gl = gl;

            string vert = @"#version 330 core
layout (location = 0) in vec2 aPos;
layout (location = 1) in vec4 aColor;
out vec4 vColor;
uniform mat4 uProjection;
uniform mat4 uView;
void main() {
    vColor = aColor;
    gl_Position = uProjection * uView * vec4(aPos, 0.0, 1.0);
    gl_PointSize = 4.0;
}";
            string frag = @"#version 330 core
in vec4 vColor;
out vec4 FragColor;
void main() {
    FragColor = vColor;
}";
            _shader = new Shader(_gl, vert, frag);

            _vao = _gl.GenVertexArray();
            _vbo = _gl.GenBuffer();

            _gl.BindVertexArray(_vao);
            _gl.BindBuffer(BufferTargetARB.ArrayBuffer, _vbo);
            unsafe {
                _gl.BufferData(BufferTargetARB.ArrayBuffer, (nuint)(MaxParticles * sizeof(Particle)), null, BufferUsageARB.DynamicDraw);

                _gl.EnableVertexAttribArray(0);
                _gl.VertexAttribPointer(0, 2, VertexAttribPointerType.Float, false, (uint)sizeof(Particle), (void*)0);

                _gl.EnableVertexAttribArray(1);
                _gl.VertexAttribPointer(1, 4, VertexAttribPointerType.Float, false, (uint)sizeof(Particle), (void*)16);
            }
        }

        public void Emit(Vector2 position, Vector2 velocity, Color color, float life)
        {
            if (_activeCount < MaxParticles)
            {
                _particles[_activeCount++] = new Particle {
                    Position = position,
                    Velocity = velocity,
                    Color = new Vector4(color.R, color.G, color.B, color.A),
                    Life = life,
                    MaxLife = life
                };
            }
        }

        public void Update(float deltaTime)
        {
            for (int i = 0; i < _activeCount; i++)
            {
                _particles[i].Position += _particles[i].Velocity * deltaTime;
                _particles[i].Life -= deltaTime;
                _particles[i].Color.W = _particles[i].Life / _particles[i].MaxLife;

                if (_particles[i].Life <= 0)
                {
                    _particles[i] = _particles[--_activeCount];
                    i--;
                }
            }
        }

        public unsafe void Render(Matrix4x4 view, Matrix4x4 projection)
        {
            if (_activeCount == 0) return;

            _shader.Use();
            _shader.SetUniform("uProjection", projection);
            _shader.SetUniform("uView", view);

            _gl.BindVertexArray(_vao);
            _gl.BindBuffer(BufferTargetARB.ArrayBuffer, _vbo);
            fixed(Particle* p = _particles)
                _gl.BufferSubData(BufferTargetARB.ArrayBuffer, 0, (nuint)(_activeCount * sizeof(Particle)), p);

            _gl.Enable(EnableCap.ProgramPointSize);
            _gl.DrawArrays(PrimitiveType.Points, 0, (uint)_activeCount);
        }

        public void Dispose()
        {
            _shader.Dispose();
            _gl.DeleteVertexArray(_vao);
            _gl.DeleteBuffer(_vbo);
        }
    }
}
