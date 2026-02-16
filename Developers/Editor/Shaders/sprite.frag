#version 330 core
out vec4 FragColor;

in vec2 TexCoord;
in vec4 vColor;

uniform sampler2D uTexture;

void main()
{
    vec4 texColor = texture(uTexture, TexCoord);
    if(texColor.a < 0.01) discard;
    FragColor = texColor * vColor;
}
