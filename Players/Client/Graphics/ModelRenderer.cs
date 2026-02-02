using Shared.Models;
using Shared.Interfaces;
using Shared.Enums;
using Shared.Operations;
using Shared.Services;
using Silk.NET.OpenGL;
using System;
using System.Collections.Generic;
using System.IO;
using System.Numerics;

namespace Client.Graphics
{
    public class ModelRenderer : IDisposable
    {
        private readonly GL _gl;
        private readonly Shader _shader;

        public ModelRenderer(GL gl)
        {
            _gl = gl;
            _shader = new Shader(_gl, File.ReadAllText("Shaders/model.vert"), File.ReadAllText("Shaders/model.frag"));
        }

        public void Render(Mesh mesh, uint textureId, Matrix4x4 model, Matrix4x4 view, Matrix4x4 projection, Vector3 color, Vector3? lightDir = null, Vector3? lightColor = null, Vector3? ambientColor = null)
        {
            _shader.Use();
            _shader.SetUniform("uModel", model);
            _shader.SetUniform("uView", view);
            _shader.SetUniform("uProjection", projection);
            _shader.SetUniform("uColor", color);

            _shader.SetUniform("uLightDir", lightDir ?? new Vector3(-0.5f, -1.0f, -0.5f));
            _shader.SetUniform("uLightColor", lightColor ?? new Vector3(1.0f, 1.0f, 1.0f));
            _shader.SetUniform("uAmbientColor", ambientColor ?? new Vector3(0.2f, 0.2f, 0.2f));

            _gl.ActiveTexture(TextureUnit.Texture0);
            _gl.BindTexture(TextureTarget.Texture2D, textureId);
            _shader.SetUniform("uTexture", 0);

            mesh.Draw();
        }

        public void Dispose()
        {
            _shader.Dispose();
        }
    }
}
