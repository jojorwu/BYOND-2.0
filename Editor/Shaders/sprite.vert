#version 330 core
layout (location = 0) in vec2 aPosition;
layout (location = 1) in vec2 aTexCoord;

out vec2 TexCoord;

uniform mat4 uProjection;
layout (location = 2) in mat4 aModel;

void main()
{
    gl_Position = uProjection * aModel * vec4(aPosition, 0.0, 1.0);
    TexCoord = aTexCoord;
}
