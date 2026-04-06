using Silk.NET.OpenGL;
using System;
using System.Collections.Generic;
using System.IO;
using System.Numerics;

namespace Client.Graphics
{
    /// <summary>
    /// Provides structured lighting data for the model renderer.
    /// </summary>
    public struct ModelLightData
    {
        public Vector3 Direction;
        public Vector3 Color;
        public Vector3 Ambient;

        public static ModelLightData Default => new()
        {
            Direction = new Vector3(-0.5f, -1.0f, -0.5f),
            Color = new Vector3(1.0f, 1.0f, 1.0f),
            Ambient = new Vector3(0.2f, 0.2f, 0.2f)
        };
    }

    /// <summary>
    /// Optimized renderer for 3D meshes using standard PBR lighting.
    /// Handles shader state and uniform management for high-performance mesh rendering.
    /// </summary>
    public class ModelRenderer : IDisposable
    {
        private readonly GL _gl;
        private readonly Shader _shader;

        public ModelRenderer(GL gl)
        {
            _gl = gl;
            _shader = new Shader(_gl, File.ReadAllText("Shaders/model.vert"), File.ReadAllText("Shaders/model.frag"));
        }

        /// <summary>
        /// Renders a mesh with the specified transform and lighting data.
        /// </summary>
        /// <param name="mesh">The mesh to render.</param>
        /// <param name="textureId">Albedo texture handle.</param>
        /// <param name="model">World transformation matrix.</param>
        /// <param name="view">View transformation matrix.</param>
        /// <param name="projection">Projection transformation matrix.</param>
        /// <param name="color">Base tint color.</param>
        /// <param name="lightData">Optional lighting parameters.</param>
        public void Render(Mesh mesh, uint textureId, Matrix4x4 model, Matrix4x4 view, Matrix4x4 projection, Vector3 color, ModelLightData? lightData = null)
        {
            _shader.Use();
            _shader.SetUniform("uModel", model);
            _shader.SetCameraMatrices(view, projection);
            _shader.SetUniform("uColor", color);

            // Correct normal mapping requires the inverse transpose of the model matrix
            if (Matrix4x4.Invert(model, out var inv))
            {
                _shader.SetUniform("uModelInvTranspose", Matrix4x4.Transpose(inv));
            }

            var lights = lightData ?? ModelLightData.Default;
            _shader.SetUniform("uLightDir", lights.Direction);
            _shader.SetUniform("uLightColor", lights.Color);
            _shader.SetUniform("uAmbientColor", lights.Ambient);

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
