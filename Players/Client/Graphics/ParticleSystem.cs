using System;
using Silk.NET.OpenGL;
using System.Numerics;
using System.Runtime.InteropServices;
using Robust.Shared.Maths;

namespace Client.Graphics
{
    /// <summary>
    /// High-performance particle system using hardware-accelerated point sprites.
    /// Utilizes Triple-Buffering and Buffer Orphaning for zero-wait GPU synchronization.
    /// </summary>
    public class ParticleSystem : IDisposable
    {
        private const int MaxParticles = 16384;
        private const int BufferCount = 3;

        private readonly GL _gl;
        private readonly Shader _shader;
        private readonly uint _vao;
        private readonly uint[] _vbos = new uint[BufferCount];
        private int _currentBufferIndex = 0;

        [StructLayout(LayoutKind.Sequential)]
        public struct Particle
        {
            public Vector2 Position;
            public Vector2 Velocity;
            public Vector4 Color;
            public float Life;
            public float MaxLife;
        }

        private readonly Particle[] _particles = new Particle[MaxParticles];
        private int _activeCount = 0;

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
            _gl.BindVertexArray(_vao);

            for (int i = 0; i < BufferCount; i++)
            {
                _vbos[i] = _gl.GenBuffer();
                _gl.BindBuffer(BufferTargetARB.ArrayBuffer, _vbos[i]);
                unsafe
                {
                    _gl.BufferData(BufferTargetARB.ArrayBuffer, (nuint)(MaxParticles * sizeof(Particle)), null, BufferUsageARB.DynamicDraw);
                }
            }

            unsafe
            {
                var size = (uint)sizeof(Particle);
                _gl.EnableVertexAttribArray(0); // Position
                _gl.VertexAttribPointer(0, 2, VertexAttribPointerType.Float, false, size, (void*)0);

                _gl.EnableVertexAttribArray(1); // Color
                _gl.VertexAttribPointer(1, 4, VertexAttribPointerType.Float, false, size, (void*)16);
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

        /// <summary>
        /// Updates the simulation of all active particles.
        /// Performs lifetime management and physics integration.
        /// </summary>
        public void Update(float deltaTime)
        {
            var particles = _particles.AsSpan(0, _activeCount);
            for (int i = 0; i < particles.Length; i++)
            {
                ref var p = ref particles[i];
                p.Position += p.Velocity * deltaTime;
                p.Life -= deltaTime;
                p.Color.W = p.Life / p.MaxLife;

                if (p.Life <= 0)
                {
                    particles[i] = particles[particles.Length - 1];
                    _activeCount--;
                    particles = _particles.AsSpan(0, _activeCount);
                    i--;
                }
            }
        }

        /// <summary>
        /// Renders all active particles to the screen.
        /// Utilizes Buffer Orphaning to avoid driver synchronization stalls.
        /// </summary>
        public unsafe void Render(Matrix4x4 view, Matrix4x4 projection)
        {
            if (_activeCount == 0) return;

            _shader.Use();
            _shader.SetCameraMatrices(view, projection);

            _gl.BindVertexArray(_vao);

            // Cycle buffers for Triple-Buffering
            _currentBufferIndex = (_currentBufferIndex + 1) % BufferCount;
            _gl.BindBuffer(BufferTargetARB.ArrayBuffer, _vbos[_currentBufferIndex]);

            // Buffer Orphaning: Reallocate current buffer to avoid stalling on previous frames
            _gl.BufferData(BufferTargetARB.ArrayBuffer, (nuint)(MaxParticles * sizeof(Particle)), null, BufferUsageARB.DynamicDraw);

            fixed (Particle* p = &_particles[0])
            {
                _gl.BufferSubData(BufferTargetARB.ArrayBuffer, 0, (nuint)(_activeCount * sizeof(Particle)), p);
            }

            // Re-bind attributes for the new VBO
            var size = (uint)sizeof(Particle);
            _gl.VertexAttribPointer(0, 2, VertexAttribPointerType.Float, false, size, (void*)0);
            _gl.VertexAttribPointer(1, 4, VertexAttribPointerType.Float, false, size, (void*)16);

            _gl.Enable(EnableCap.ProgramPointSize);
            _gl.DrawArrays(PrimitiveType.Points, 0, (uint)_activeCount);
        }

        public void Dispose()
        {
            _shader.Dispose();
            _gl.DeleteVertexArray(_vao);
            for (int i = 0; i < BufferCount; i++)
            {
                _gl.DeleteBuffer(_vbos[i]);
            }
        }
    }
}
