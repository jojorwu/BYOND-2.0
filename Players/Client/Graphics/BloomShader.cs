using System;
using Silk.NET.OpenGL;
using System.Numerics;
using Robust.Shared.Maths;

namespace Client.Graphics
{
    public class BloomShader : IDisposable
    {
        private readonly GL _gl;
        private readonly Shader _extractShader;
        private readonly Shader _blurShader;

        public BloomShader(GL gl)
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

            string extractFrag = @"#version 330 core
out vec4 FragColor;
in vec2 TexCoords;
uniform sampler2D uScreenTexture;
void main() {
    vec3 color = texture(uScreenTexture, TexCoords).rgb;
    float brightness = dot(color, vec3(0.2126, 0.7152, 0.0722));

    // Smooth thresholding
    float threshold = 0.75;
    float softThreshold = 0.1;
    float knee = max(0.0, brightness - threshold + softThreshold);
    knee = (knee * knee) / (4.0 * softThreshold);
    float weight = max(knee, brightness - threshold) / max(brightness, 0.0001);

    FragColor = vec4(color * weight, 1.0);
}";

            string blurFrag = @"#version 330 core
out vec4 FragColor;
in vec2 TexCoords;
uniform sampler2D uImage;
uniform bool uHorizontal;
uniform float uWeight[9] = float[] (0.15317, 0.144893, 0.122649, 0.092902, 0.06297, 0.038146, 0.020621, 0.009968, 0.004313);

void main() {
    vec2 tex_offset = 1.0 / textureSize(uImage, 0);
    vec3 result = texture(uImage, TexCoords).rgb * uWeight[0];
    if(uHorizontal) {
        for(int i = 1; i < 9; ++i) {
            result += texture(uImage, TexCoords + vec2(tex_offset.x * i, 0.0)).rgb * uWeight[i];
            result += texture(uImage, TexCoords - vec2(tex_offset.x * i, 0.0)).rgb * uWeight[i];
        }
    } else {
        for(int i = 1; i < 9; ++i) {
            result += texture(uImage, TexCoords + vec2(0.0, tex_offset.y * i)).rgb * uWeight[i];
            result += texture(uImage, TexCoords - vec2(0.0, tex_offset.y * i)).rgb * uWeight[i];
        }
    }
    FragColor = vec4(result, 1.0);
}";

            _extractShader = new Shader(_gl, vert, extractFrag);
            _blurShader = new Shader(_gl, vert, blurFrag);
        }

        public void ExtractBright(uint screenTexture)
        {
            _extractShader.Use();
            _gl.ActiveTexture(TextureUnit.Texture0);
            _gl.BindTexture(TextureTarget.Texture2D, screenTexture);
            _extractShader.SetUniform("uScreenTexture", 0);
            // Render full screen quad...
        }

        public void Blur(uint texture, bool horizontal)
        {
            _blurShader.Use();
            _gl.ActiveTexture(TextureUnit.Texture0);
            _gl.BindTexture(TextureTarget.Texture2D, texture);
            _blurShader.SetUniform("uImage", 0);
            _blurShader.SetUniform("uHorizontal", horizontal ? 1 : 0);
            // Render full screen quad...
        }

        public void Dispose()
        {
            _extractShader.Dispose();
            _blurShader.Dispose();
        }
    }
}
