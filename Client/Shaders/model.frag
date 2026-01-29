#version 330 core
out vec4 FragColor;

in vec3 Normal;
in vec2 TexCoords;

uniform sampler2D uTexture;
uniform vec3 uColor;

uniform vec3 uLightDir;
uniform vec3 uLightColor;
uniform vec3 uAmbientColor;

void main()
{
    vec3 norm = normalize(Normal);
    vec3 lightDir = normalize(-uLightDir);
    float diff = max(dot(norm, lightDir), 0.0);
    vec3 diffuse = diff * uLightColor;

    vec4 texColor = texture(uTexture, TexCoords);
    vec3 result = (uAmbientColor + diffuse) * uColor * texColor.rgb;
    FragColor = vec4(result, texColor.a);
}
