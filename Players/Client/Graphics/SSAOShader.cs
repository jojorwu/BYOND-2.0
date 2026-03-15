using System;
using Silk.NET.OpenGL;
using System.Numerics;

namespace Client.Graphics
{
    public class SSAOShader : IDisposable
    {
        private readonly GL _gl;
        private readonly Shader _shader;

        public SSAOShader(GL gl)
        {
            _gl = gl;

            string vert = @"#version 330 core
layout (location = 0) in vec2 aPos;
layout (location = 1) in vec2 aTexCoords;
out vec2 TexCoords;
void main() {
    TexCoords = aTexCoords;
    gl_Position = vec4(aPos, 0.0, 1.0);
}";
            string frag = @"#version 330 core
out vec4 FragColor;
in vec2 TexCoords;
uniform sampler2D uOccluderMap;
uniform sampler2D uDepthMap;
uniform float uRadius;
uniform float uTime;

float rand(vec2 co) {
    return fract(sin(dot(co.xy ,vec2(12.9898,78.233))) * 43758.5453);
}

void main() {
    float occlusion = 0.0;
    vec2 tex_offset = 1.0 / textureSize(uOccluderMap, 0);
    float depth = texture(uDepthMap, TexCoords).r;

    // Poisson disk-like sampling for softer AO
    const int SAMPLES = 16;
    vec2 samples[16] = vec2[](
        vec2(1, 0), vec2(-1, 0), vec2(0, 1), vec2(0, -1),
        vec2(0.7, 0.7), vec2(-0.7, 0.7), vec2(0.7, -0.7), vec2(-0.7, -0.7),
        vec2(0.5, 0), vec2(-0.5, 0), vec2(0, 0.5), vec2(0, -0.5),
        vec2(0.35, 0.35), vec2(-0.35, 0.35), vec2(0.35, -0.35), vec2(-0.35, -0.35)
    );

    float noise = rand(TexCoords + uTime);

    for (int i = 0; i < SAMPLES; i++) {
        vec2 offset = samples[i] * uRadius * tex_offset;
        // Rotate samples by noise
        float s = sin(noise * 6.28);
        float c = cos(noise * 6.28);
        offset = vec2(offset.x * c - offset.y * s, offset.x * s + offset.y * c);

        if (texture(uOccluderMap, TexCoords + offset).r > 0.5) {
            float sampleDepth = texture(uDepthMap, TexCoords + offset).r;
            float rangeCheck = smoothstep(0.0, 1.0, uRadius / max(0.001, abs(depth - sampleDepth)));
            occlusion += (1.0 / float(SAMPLES)) * rangeCheck;
        }
    }

    float ao = 1.0 - clamp(occlusion * 2.0, 0.0, 0.8);
    FragColor = vec4(vec3(ao), 1.0);
}";

            _shader = new Shader(_gl, vert, frag);
        }

        public void Use(uint occluderMap, uint depthMap, float radius)
        {
            _shader.Use();
            _gl.ActiveTexture(TextureUnit.Texture0);
            _gl.BindTexture(TextureTarget.Texture2D, occluderMap);
            _shader.SetUniform("uOccluderMap", 0);

            _gl.ActiveTexture(TextureUnit.Texture1);
            _gl.BindTexture(TextureTarget.Texture2D, depthMap);
            _shader.SetUniform("uDepthMap", 1);

            _shader.SetUniform("uRadius", radius);
            _shader.SetUniform("uTime", (float)DateTime.Now.TimeOfDay.TotalSeconds);
        }

        public void Dispose()
        {
            _shader.Dispose();
        }
    }
}
