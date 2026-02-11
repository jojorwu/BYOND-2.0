#version 330 core
layout (location = 0) out vec4 gAlbedo;
layout (location = 1) out vec4 gNormal;

in vec2 TexCoord;
in vec4 vColor;

uniform sampler2D uTexture;
uniform sampler2D uNormalMap;
uniform bool uHasNormalMap;

void main()
{
    vec4 texColor = texture(uTexture, TexCoord);
    if(texColor.a < 0.01)
        discard;

    gAlbedo = texColor * vColor;

    if (uHasNormalMap) {
        gNormal = texture(uNormalMap, TexCoord);
    } else {
        gNormal = vec4(0.5, 0.5, 1.0, 1.0); // Flat normal
    }
}
