#version 330 core
out vec4 FragColor;

in vec3 Normal;
in vec2 TexCoords;

uniform sampler2D uTexture;
uniform vec3 uColor;

void main()
{
    FragColor = texture(uTexture, TexCoords) * vec4(uColor, 1.0);
}
