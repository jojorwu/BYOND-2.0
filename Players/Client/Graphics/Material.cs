using Shared.Models;
using Shared.Interfaces;
using Shared.Enums;
using Shared.Operations;
using Shared.Services;
using System.Collections.Generic;
using System.Numerics;

namespace Client.Graphics
{
    public class Material
    {
        public Shader Shader { get; }
        public Dictionary<string, object> Parameters { get; } = new();

        public Material(Shader shader)
        {
            Shader = shader;
        }

        public void Apply()
        {
            Shader.Use();
            foreach (var parameter in Parameters)
            {
                if (parameter.Value is float f) Shader.SetUniform(parameter.Key, f);
                else if (parameter.Value is int i) Shader.SetUniform(parameter.Key, i);
                else if (parameter.Value is Vector3 v) Shader.SetUniform(parameter.Key, v);
                else if (parameter.Value is Matrix4x4 m) Shader.SetUniform(parameter.Key, m);
            }
        }
    }
}
