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
out vec2 WorldPos;
uniform mat4 uProjection;
uniform mat4 uView;
uniform mat4 uModel;
void main() {
    TexCoords = aTexCoords;
    vec4 worldPos = uModel * vec4(aPos, 0.0, 1.0);
    WorldPos = worldPos.xy;
    gl_Position = uProjection * uView * worldPos;
}";
            string frag = @"#version 330 core
out vec4 FragColor;
in vec2 TexCoords;
in vec2 WorldPos;
uniform vec4 uColor;
uniform vec2 uLightPos;
uniform float uRadius;
uniform sampler2D uNormalBuffer;
uniform sampler2D uOccluderMap;
uniform sampler2D uAlbedoBuffer;
uniform sampler2D uPbrBuffer;
uniform vec4 uScreenBounds; // left, top, right, bottom

void main() {
    float dist = length(TexCoords - vec2(0.5));
    if (dist > 0.5) discard;

    float alpha = 1.0 - smoothstep(0.0, 0.5, dist);
    vec2 screenUV = (WorldPos - uScreenBounds.xy) / (uScreenBounds.zw - uScreenBounds.xy);

    // PBR Shading
    vec3 albedo = texture(uAlbedoBuffer, screenUV).rgb;
    vec3 normal = texture(uNormalBuffer, screenUV).rgb * 2.0 - 1.0;
    vec2 pbr = texture(uPbrBuffer, screenUV).rg;
    float metallic = pbr.r;
    float roughness = pbr.g;

    vec3 lightDir = normalize(vec3(uLightPos - WorldPos, 32.0));
    vec3 viewDir = vec3(0.0, 0.0, 1.0); // Orthographic view
    vec3 halfDir = normalize(lightDir + viewDir);

    float diff = max(dot(normal, lightDir), 0.0);

    // Simple Cook-Torrance subset
    float NDF = pow(max(dot(normal, halfDir), 0.0), mix(100.0, 1.0, roughness));
    float spec = NDF * metallic;

    float intensity = alpha * (diff + spec);
    if (intensity < 0.01) discard;

    // Enhanced Shadow Raycasting (Jittered for Soft Shadows)
    float dToLight = distance(WorldPos, uLightPos);
    float shadow = 1.0;

    if (dToLight > 4.0) {
        vec2 dir = normalize(uLightPos - WorldPos);
        float noise = fract(sin(dot(WorldPos, vec2(12.9898, 78.233))) * 43758.5453);

        // Soft shadow jitter
        float jitter = (noise - 0.5) * 2.0;
        vec2 jitteredDir = normalize(dir + vec2(-dir.y, dir.x) * jitter * 0.05);

        float stepSize = 4.0;
        for (float i = 4.0; i < dToLight - 4.0; i += stepSize) {
            vec2 samplePos = WorldPos + jitteredDir * i;
            vec2 uv = (samplePos - uScreenBounds.xy) / (uScreenBounds.zw - uScreenBounds.xy);
            if (uv.x >= 0.0 && uv.x <= 1.0 && uv.y >= 0.0 && uv.y <= 1.0) {
                if (texture(uOccluderMap, uv).r > 0.5) {
                    shadow = 0.0;
                    break;
                }
            }
        }
    }

    // Physically-based falloff (inverse square with smoothing)
    float falloff = pow(clamp(1.0 - pow(dToLight / uRadius, 4.0), 0.0, 1.0), 2.0) / (dToLight * dToLight + 1.0);
    FragColor = vec4(uColor.rgb, uColor.a * intensity * shadow * falloff);
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

        public void Render(Matrix4x4 view, Matrix4x4 projection, uint normalBuffer, uint occluderMap, uint albedoBuffer, uint pbrBuffer, Box2 screenBounds)
        {
            if (_lights.Count == 0) return;

            _lightingShader.Use();
            _lightingShader.SetUniform("uProjection", projection);
            _lightingShader.SetUniform("uView", view);
            _lightingShader.SetUniform("uNormalBuffer", 0);
            _lightingShader.SetUniform("uOccluderMap", 1);
            _lightingShader.SetUniform("uAlbedoBuffer", 2);
            _lightingShader.SetUniform("uPbrBuffer", 3);
            _lightingShader.SetUniform("uScreenBounds", new Vector4(screenBounds.Left, screenBounds.Top, screenBounds.Right, screenBounds.Bottom));

            _gl.ActiveTexture(TextureUnit.Texture0);
            _gl.BindTexture(TextureTarget.Texture2D, normalBuffer);
            _gl.ActiveTexture(TextureUnit.Texture1);
            _gl.BindTexture(TextureTarget.Texture2D, occluderMap);
            _gl.ActiveTexture(TextureUnit.Texture2);
            _gl.BindTexture(TextureTarget.Texture2D, albedoBuffer);
            _gl.ActiveTexture(TextureUnit.Texture3);
            _gl.BindTexture(TextureTarget.Texture2D, pbrBuffer);

            _gl.BindVertexArray(_vao);
            _gl.Enable(EnableCap.Blend);
            _gl.BlendFunc(BlendingFactor.SrcAlpha, BlendingFactor.One);

            foreach (var light in _lights)
            {
                // Frustum Culling for Lights
                if (light.Position.X + light.Radius < screenBounds.Left || light.Position.X - light.Radius > screenBounds.Right ||
                    light.Position.Y + light.Radius < screenBounds.Top || light.Position.Y - light.Radius > screenBounds.Bottom)
                {
                    continue;
                }

                var model = Matrix4x4.CreateScale(light.Radius * 2) * Matrix4x4.CreateTranslation(light.Position.X, light.Position.Y, 0);
                _lightingShader.SetUniform("uModel", model);
                _lightingShader.SetUniform("uColor", light.Color);
                _lightingShader.SetUniform("uLightPos", light.Position);
                _lightingShader.SetUniform("uRadius", light.Radius);
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
