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
uniform float uRadius;

void main() {
    float occlusion = 0.0;
    vec2 tex_offset = 1.0 / textureSize(uOccluderMap, 0);

    // Sample a small neighborhood
    for (int x = -2; x <= 2; x++) {
        for (int y = -2; y <= 2; y++) {
            if (texture(uOccluderMap, TexCoords + vec2(x, y) * tex_offset).r > 0.5) {
                occlusion += 0.04; // Weighted contribution
            }
        }
    }

    float ao = 1.0 - clamp(occlusion, 0.0, 0.5);
    FragColor = vec4(vec3(ao), 1.0);
}";

            _shader = new Shader(_gl, vert, frag);
        }

        public void Use(uint occluderMap)
        {
            _shader.Use();
            _gl.ActiveTexture(TextureUnit.Texture0);
            _gl.BindTexture(TextureTarget.Texture2D, occluderMap);
            _shader.SetUniform("uOccluderMap", 0);
        }

        public void Dispose()
        {
            _shader.Dispose();
        }
    }
}
