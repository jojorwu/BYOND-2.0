using System;
using System.Collections.Generic;
using Silk.NET.OpenGL;
using System.Numerics;
using Robust.Shared.Maths;

namespace Client.Graphics
{
    public class LightingRenderer : IDisposable
    {
        private readonly GL _gl;
        private readonly Shader _lightingShader;
        private readonly uint _vao;
        private readonly uint _vbo;
        private readonly List<LightSource> _lights = new();

        public struct LightSource
        {
            public Vector2 Position;
            public float Radius;
            public Color Color;
        }

        public LightingRenderer(GL gl)
        {
            _gl = gl;

            string vert = @"#version 330 core
layout (location = 0) in vec2 aPos;
layout (location = 1) in vec2 aTexCoords;
out vec2 TexCoords;
uniform mat4 uProjection;
uniform mat4 uView;
uniform mat4 uModel;
void main() {
    TexCoords = aTexCoords;
    gl_Position = uProjection * uView * uModel * vec4(aPos, 0.0, 1.0);
}";
            string frag = @"#version 330 core
out vec4 FragColor;
in vec2 TexCoords;
uniform vec4 uColor;
void main() {
    float dist = length(TexCoords - vec2(0.5));
    float alpha = 1.0 - smoothstep(0.0, 0.5, dist);
    FragColor = vec4(uColor.rgb, uColor.a * alpha);
}";

            _lightingShader = new Shader(_gl, vert, frag);

            _vao = _gl.GenVertexArray();
            _vbo = _gl.GenBuffer();

            _gl.BindVertexArray(_vao);
            _gl.BindBuffer(BufferTargetARB.ArrayBuffer, _vbo);

            float[] quad = {
                -0.5f, -0.5f, 0f, 0f,
                 0.5f, -0.5f, 1f, 0f,
                 0.5f,  0.5f, 1f, 1f,
                 0.5f,  0.5f, 1f, 1f,
                -0.5f,  0.5f, 0f, 1f,
                -0.5f, -0.5f, 0f, 0f
            };

            unsafe {
                fixed(float* p = quad)
                    _gl.BufferData(BufferTargetARB.ArrayBuffer, (nuint)(quad.Length * sizeof(float)), p, BufferUsageARB.StaticDraw);

                _gl.EnableVertexAttribArray(0);
                _gl.VertexAttribPointer(0, 2, VertexAttribPointerType.Float, false, 4 * sizeof(float), (void*)0);
                _gl.EnableVertexAttribArray(1);
                _gl.VertexAttribPointer(1, 2, VertexAttribPointerType.Float, false, 4 * sizeof(float), (void*)(2 * sizeof(float)));
            }
        }

        public void AddLight(Vector2 position, float radius, Color color)
        {
            _lights.Add(new LightSource { Position = position, Radius = radius, Color = color });
        }

        public void Render(Matrix4x4 view, Matrix4x4 projection)
        {
            _lightingShader.Use();
            _lightingShader.SetUniform("uProjection", projection);
            _lightingShader.SetUniform("uView", view);

            _gl.BindVertexArray(_vao);
            _gl.Enable(EnableCap.Blend);
            _gl.BlendFunc(BlendingFactor.SrcAlpha, BlendingFactor.One);

            foreach (var light in _lights)
            {
                var model = Matrix4x4.CreateScale(light.Radius * 2) * Matrix4x4.CreateTranslation(light.Position.X, light.Position.Y, 0);
                _lightingShader.SetUniform("uModel", model);
                _lightingShader.SetUniform("uColor", light.Color);
                _gl.DrawArrays(PrimitiveType.Triangles, 0, 6);
            }

            _lights.Clear();
        }

        public void Dispose()
        {
            _lightingShader.Dispose();
            _gl.DeleteVertexArray(_vao);
            _gl.DeleteBuffer(_vbo);
        }
    }
}
