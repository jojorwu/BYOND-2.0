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
void main() {
    vec3 color = texture(uScreenTexture, TexCoords).rgb;
    // Simple Contrast & Saturation enhancement
    color = (color - 0.5) * 1.1 + 0.5;
    float luminance = dot(color, vec3(0.2126, 0.7152, 0.0722));
    color = mix(vec3(luminance), color, 1.2);
    FragColor = vec4(color, 1.0);
}";

            _shader = new Shader(_gl, vert, frag);
        }

        public void Use(uint screenTexture)
        {
            _shader.Use();
            _gl.ActiveTexture(TextureUnit.Texture0);
            _gl.BindTexture(TextureTarget.Texture2D, screenTexture);
            _shader.SetUniform("uScreenTexture", 0);
        }

        public void Dispose()
        {
            _shader.Dispose();
        }
    }
}
