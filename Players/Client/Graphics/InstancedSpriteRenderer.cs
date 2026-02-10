using System;
using System.Collections.Generic;
using Silk.NET.OpenGL;
using System.Numerics;
using System.Runtime.InteropServices;
using Robust.Shared.Maths;

namespace Client.Graphics
{
    public class InstancedSpriteRenderer : IDisposable
    {
        private readonly GL _gl;
        private readonly Shader _shader;
        private readonly uint _vao;
        private readonly uint _vbo; // Quad vertices
        private readonly uint _instanceVbo; // Instance data (Position, UV, Color)

        [StructLayout(LayoutKind.Sequential)]
        public struct InstanceData
        {
            public Vector4 Rect; // X, Y, Width, Height
            public Vector4 Uv;   // Left, Top, Right, Bottom
            public Color Color;
        }

        private const int MaxInstances = 4096;
        private readonly InstanceData[] _instanceData = new InstanceData[MaxInstances];
        private int _instanceCount = 0;

        public InstancedSpriteRenderer(GL gl)
        {
            _gl = gl;

            string vert = @"#version 330 core
layout (location = 0) in vec2 aPos;
layout (location = 1) in vec4 iRect;
layout (location = 2) in vec4 iUv;
layout (location = 3) in vec4 iColor;
out vec2 TexCoords;
out vec4 Color;
uniform mat4 uProjection;
uniform mat4 uView;
void main() {
    vec2 worldPos = iRect.xy + aPos * iRect.zw;
    TexCoords = iUv.xy + aPos * (iUv.zw - iUv.xy);
    Color = iColor;
    gl_Position = uProjection * uView * vec4(worldPos, 0.0, 1.0);
}";
            string frag = @"#version 330 core
out vec4 FragColor;
in vec2 TexCoords;
in vec4 Color;
uniform sampler2D uTexture;
void main() {
    FragColor = texture(uTexture, TexCoords) * Color;
}";

            _shader = new Shader(_gl, vert, frag);

            _vao = _gl.GenVertexArray();
            _vbo = _gl.GenBuffer();
            _instanceVbo = _gl.GenBuffer();

            _gl.BindVertexArray(_vao);

            float[] quad = { 0, 0, 1, 0, 1, 1, 1, 1, 0, 1, 0, 0 };
            _gl.BindBuffer(BufferTargetARB.ArrayBuffer, _vbo);
            unsafe {
                fixed(float* p = quad)
                    _gl.BufferData(BufferTargetARB.ArrayBuffer, (nuint)(quad.Length * sizeof(float)), p, BufferUsageARB.StaticDraw);

                _gl.EnableVertexAttribArray(0);
                _gl.VertexAttribPointer(0, 2, VertexAttribPointerType.Float, false, 2 * sizeof(float), (void*)0);
            }

            _gl.BindBuffer(BufferTargetARB.ArrayBuffer, _instanceVbo);
            unsafe {
                _gl.BufferData(BufferTargetARB.ArrayBuffer, (nuint)(MaxInstances * sizeof(InstanceData)), null, BufferUsageARB.DynamicDraw);

                // Location 1: iRect
                _gl.EnableVertexAttribArray(1);
                _gl.VertexAttribPointer(1, 4, VertexAttribPointerType.Float, false, (uint)sizeof(InstanceData), (void*)0);
                _gl.VertexAttribDivisor(1, 1);

                // Location 2: iUv
                _gl.EnableVertexAttribArray(2);
                _gl.VertexAttribPointer(2, 4, VertexAttribPointerType.Float, false, (uint)sizeof(InstanceData), (void*)16);
                _gl.VertexAttribDivisor(2, 1);

                // Location 3: iColor
                _gl.EnableVertexAttribArray(3);
                _gl.VertexAttribPointer(3, 4, VertexAttribPointerType.Float, false, (uint)sizeof(InstanceData), (void*)32);
                _gl.VertexAttribDivisor(3, 1);
            }

            _gl.BindVertexArray(0);
        }

        public void Draw(uint textureId, Vector2 position, Vector2 size, Box2 uv, Color color, Matrix4x4 view, Matrix4x4 projection)
        {
            if (_instanceCount >= MaxInstances) Flush(textureId, view, projection);

            _instanceData[_instanceCount++] = new InstanceData {
                Rect = new Vector4(position.X, position.Y, size.X, size.Y),
                Uv = new Vector4(uv.Left, uv.Top, uv.Right, uv.Bottom),
                Color = color
            };
        }

        public unsafe void Flush(uint textureId, Matrix4x4 view, Matrix4x4 projection)
        {
            if (_instanceCount == 0) return;

            _shader.Use();
            _shader.SetUniform("uView", view);
            _shader.SetUniform("uProjection", projection);
            _gl.ActiveTexture(TextureUnit.Texture0);
            _gl.BindTexture(TextureTarget.Texture2D, textureId);
            _shader.SetUniform("uTexture", 0);

            _gl.BindVertexArray(_vao);
            _gl.BindBuffer(BufferTargetARB.ArrayBuffer, _instanceVbo);
            fixed(InstanceData* p = _instanceData)
                _gl.BufferSubData(BufferTargetARB.ArrayBuffer, 0, (nuint)(_instanceCount * sizeof(InstanceData)), p);

            _gl.DrawArraysInstanced(PrimitiveType.Triangles, 0, 6, (uint)_instanceCount);

            _instanceCount = 0;
        }

        public void Dispose()
        {
            _shader.Dispose();
            _gl.DeleteVertexArray(_vao);
            _gl.DeleteBuffer(_vbo);
            _gl.DeleteBuffer(_instanceVbo);
        }
    }
}
