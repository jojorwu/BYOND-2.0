using System;
using Silk.NET.OpenGL;
using System.Numerics;

namespace Client.Graphics
{
    public class PostProcessShader : IDisposable
    {
        private readonly GL _gl;
        private readonly Shader _shader;

        public PostProcessShader(GL gl)
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
uniform sampler2D uScreenTexture;
uniform sampler2D uHistoryTexture;
uniform vec2 uTexelSize;
uniform float uFeedback; // History blend weight

void main() {
    vec3 color = texture(uScreenTexture, TexCoords).rgb;

    // Simple Sharpening
    vec3 neighbor = texture(uScreenTexture, TexCoords + vec2(uTexelSize.x, 0.0)).rgb;
    neighbor += texture(uScreenTexture, TexCoords - vec2(uTexelSize.x, 0.0)).rgb;
    neighbor += texture(uScreenTexture, TexCoords + vec2(0.0, uTexelSize.y)).rgb;
    neighbor += texture(uScreenTexture, TexCoords - vec2(0.0, uTexelSize.y)).rgb;
    color = color * 2.0 - neighbor * 0.25;

    // Chromatic Aberration
    float caOffset = length(TexCoords - 0.5) * 0.003;
    float red = texture(uScreenTexture, TexCoords + vec2(caOffset, 0.0)).r;
    float green = texture(uScreenTexture, TexCoords).g;
    float blue = texture(uScreenTexture, TexCoords - vec2(caOffset, 0.0)).b;
    color = mix(color, vec3(red, green, blue), 0.5);

    // TAA: Sample history with slight jitter compensation (basic version)
    vec3 history = texture(uHistoryTexture, TexCoords).rgb;
    color = mix(color, history, uFeedback);

    // ACES Filmic Tone Mapping
    const float a = 2.51;
    const float b = 0.03;
    const float c = 2.43;
    const float d = 0.59;
    const float e = 0.14;
    color = clamp((color * (a * color + b)) / (color * (c * color + d) + e), 0.0, 1.0);

    // Gamma Correction
    color = pow(color, vec3(1.0 / 2.2));

    // Contrast & Saturation enhancement
    color = (color - 0.5) * 1.15 + 0.5;
    float luminance = dot(color, vec3(0.2126, 0.7152, 0.0722));
    color = mix(vec3(luminance), color, 1.25);

    // Subtle Vignette
    vec2 uv = TexCoords * (1.0 - TexCoords.yx);
    float vig = uv.x * uv.y * 15.0;
    vig = pow(vig, 0.15);
    color *= vig;

    // Film Grain
    float noise = (fract(sin(dot(TexCoords, vec2(12.9898, 78.233))) * 43758.5453) - 0.5) * 0.02;
    color += noise;

    FragColor = vec4(color, 1.0);
}";

            _shader = new Shader(_gl, vert, frag);
        }

        public void Use(uint screenTexture, uint historyTexture, Vector2 texelSize, float feedback)
        {
            _shader.Use();
            _gl.ActiveTexture(TextureUnit.Texture0);
            _gl.BindTexture(TextureTarget.Texture2D, screenTexture);
            _shader.SetUniform("uScreenTexture", 0);

            _gl.ActiveTexture(TextureUnit.Texture1);
            _gl.BindTexture(TextureTarget.Texture2D, historyTexture);
            _shader.SetUniform("uHistoryTexture", 1);

            _shader.SetUniform("uTexelSize", texelSize);
            _shader.SetUniform("uFeedback", feedback);
        }

        public void Dispose()
        {
            _shader.Dispose();
        }
    }
}
