using System.Collections.Generic;
using System.Numerics;

namespace Client.Graphics
{
    public class Material
    {
        public Shader Shader { get; set; }
        public ICSharpShader? CSharpShader { get; set; }
        public Dictionary<string, object> Parameters { get; } = new();

        public Material(Shader shader)
        {
            Shader = shader;
        }

        public void Apply(float deltaTime = 0f)
        {
            if (CSharpShader != null)
            {
                CSharpShader.Update(Shader, deltaTime);
            }

            Shader.Use();
            foreach (var parameter in Parameters)
            {
                if (parameter.Value is float f) Shader.SetUniform(parameter.Key, f);
                else if (parameter.Value is int i) Shader.SetUniform(parameter.Key, i);
                else if (parameter.Value is Vector3 v) Shader.SetUniform(parameter.Key, v);
                else if (parameter.Value is Matrix4x4 m) Shader.SetUniform(parameter.Key, m);
                else if (parameter.Value is Vector2 v2) Shader.SetUniform(parameter.Key, v2);
                else if (parameter.Value is Vector4 v4) Shader.SetUniform(parameter.Key, v4);
                else if (parameter.Value is Robust.Shared.Maths.Color color) Shader.SetUniform(parameter.Key, color);
            }
        }
    }
}
