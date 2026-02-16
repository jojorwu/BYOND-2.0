#version 330 core
layout (location = 0) in vec2 aPosition;
layout (location = 1) in vec2 aTexCoord;
layout (location = 2) in vec4 aColor;

out vec2 TexCoord;
out vec4 vColor;

uniform mat4 uProjection;
uniform mat4 uModel;

void main()
{
    gl_Position = uProjection * uModel * vec4(aPosition, 0.0, 1.0);
    TexCoord = aTexCoord;
    vColor = aColor;
}
