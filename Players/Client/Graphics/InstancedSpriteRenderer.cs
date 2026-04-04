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
        private readonly uint _vbo;
        private uint _instanceVbo;

        [StructLayout(LayoutKind.Sequential)]
        public struct InstanceData
        {
            public Vector4 Rect;
            public Vector4 Uv;
            public Color Color;
            public float TextureLayer; // For TextureArray
            public float Padding1, Padding2, Padding3;
        }

        private struct Batch
        {
            public uint TextureArrayId;
            public int Start;
            public int Count;
        }

        private int _maxInstances = 4096;
        private InstanceData[] _instanceData;
        private int _instanceCount = 0;
        private readonly List<Batch> _batches = new();
        private uint _currentTextureArrayId;

        public InstancedSpriteRenderer(GL gl)
        {
            _gl = gl;
            _instanceData = new InstanceData[_maxInstances];

            string vert = @"#version 330 core
layout (location = 0) in vec2 aPos;
layout (location = 1) in vec4 iRect;
layout (location = 2) in vec4 iUv;
layout (location = 3) in vec4 iColor;
layout (location = 4) in float iLayer;
out vec2 TexCoords;
out vec4 vColor;
out float vLayer;
uniform mat4 uProjection;
uniform mat4 uView;
void main() {
    vec2 worldPos = iRect.xy + aPos * iRect.zw;
    TexCoords = iUv.xy + aPos * (iUv.zw - iUv.xy);
    vColor = iColor;
    vLayer = iLayer;
    gl_Position = uProjection * uView * vec4(worldPos, 0.0, 1.0);
}";
            string frag = @"#version 330 core
layout (location = 0) out vec4 gAlbedo;
in vec2 TexCoords;
in vec4 vColor;
in float vLayer;
uniform sampler2DArray uTextureArray;

void main() {
    vec4 texColor = texture(uTextureArray, vec3(TexCoords, vLayer));
    if(texColor.a < 0.01) discard;
    gAlbedo = texColor * vColor;
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
                _gl.BufferData(BufferTargetARB.ArrayBuffer, (nuint)(_maxInstances * sizeof(InstanceData)), null, BufferUsageARB.DynamicDraw);

                _gl.EnableVertexAttribArray(1);
                _gl.VertexAttribPointer(1, 4, VertexAttribPointerType.Float, false, (uint)sizeof(InstanceData), (void*)0);
                _gl.VertexAttribDivisor(1, 1);

                _gl.EnableVertexAttribArray(2);
                _gl.VertexAttribPointer(2, 4, VertexAttribPointerType.Float, false, (uint)sizeof(InstanceData), (void*)16);
                _gl.VertexAttribDivisor(2, 1);

                _gl.EnableVertexAttribArray(3);
                _gl.VertexAttribPointer(3, 4, VertexAttribPointerType.Float, false, (uint)sizeof(InstanceData), (void*)32);
                _gl.VertexAttribDivisor(3, 1);

                _gl.EnableVertexAttribArray(4);
                _gl.VertexAttribPointer(4, 1, VertexAttribPointerType.Float, false, (uint)sizeof(InstanceData), (void*)48);
                _gl.VertexAttribDivisor(4, 1);
            }

            _gl.BindVertexArray(0);
        }

        private void EnsureCapacity(int count)
        {
            if (_instanceCount + count <= _maxInstances) return;

            while (_instanceCount + count > _maxInstances) _maxInstances *= 2;

            Array.Resize(ref _instanceData, _maxInstances);
            _gl.BindBuffer(BufferTargetARB.ArrayBuffer, _instanceVbo);
            unsafe {
                _gl.BufferData(BufferTargetARB.ArrayBuffer, (nuint)(_maxInstances * sizeof(InstanceData)), null, BufferUsageARB.DynamicDraw);
            }
        }

        public void Begin()
        {
            _instanceCount = 0;
            _batches.Clear();
            _currentTextureArrayId = 0;
        }

        public void Draw(uint textureArrayId, int layer, Vector2 position, Vector2 size, Box2 uv, Color color)
        {
            if (textureArrayId == 0) return;

            if (_batches.Count == 0 || textureArrayId != _currentTextureArrayId)
            {
                _batches.Add(new Batch {
                    TextureArrayId = textureArrayId,
                    Start = _instanceCount,
                    Count = 0
                });
                _currentTextureArrayId = textureArrayId;
            }

            EnsureCapacity(1);

            _instanceData[_instanceCount++] = new InstanceData {
                Rect = new Vector4(position.X, position.Y, size.X, size.Y),
                Uv = new Vector4(uv.Left, uv.Top, uv.Right, uv.Bottom),
                Color = color,
                TextureLayer = layer
            };

            var lastBatch = _batches[_batches.Count - 1];
            lastBatch.Count++;
            _batches[_batches.Count - 1] = lastBatch;
        }

        public unsafe void Flush(Matrix4x4 view, Matrix4x4 projection)
        {
            if (_instanceCount == 0) return;

            _gl.BindBuffer(BufferTargetARB.ArrayBuffer, _instanceVbo);
            _gl.BufferData(BufferTargetARB.ArrayBuffer, (nuint)(_maxInstances * sizeof(InstanceData)), null, BufferUsageARB.StreamDraw);

            fixed(InstanceData* p = _instanceData)
                _gl.BufferSubData(BufferTargetARB.ArrayBuffer, 0, (nuint)(_instanceCount * sizeof(InstanceData)), p);

            _shader.Use();
            _shader.SetUniform("uView", view);
            _shader.SetUniform("uProjection", projection);

            _gl.BindVertexArray(_vao);

            foreach (var batch in _batches)
            {
                _gl.ActiveTexture(TextureUnit.Texture0);
                _gl.BindTexture(TextureTarget.Texture2DArray, batch.TextureArrayId);
                _shader.SetUniform("uTextureArray", 0);

                unsafe {
                    nuint offset = (nuint)(batch.Start * sizeof(InstanceData));
                    _gl.VertexAttribPointer(1, 4, VertexAttribPointerType.Float, false, (uint)sizeof(InstanceData), (void*)offset);
                    _gl.VertexAttribPointer(2, 4, VertexAttribPointerType.Float, false, (uint)sizeof(InstanceData), (void*)(offset + 16));
                    _gl.VertexAttribPointer(3, 4, VertexAttribPointerType.Float, false, (uint)sizeof(InstanceData), (void*)(offset + 32));
                    _gl.VertexAttribPointer(4, 1, VertexAttribPointerType.Float, false, (uint)sizeof(InstanceData), (void*)(offset + 48));
                }

                _gl.DrawArraysInstanced(PrimitiveType.Triangles, 0, 6, (uint)batch.Count);
            }

            _instanceCount = 0;
            _batches.Clear();
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
