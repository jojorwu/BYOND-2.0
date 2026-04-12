using System;
using System.IO;
using System.Threading.Tasks;
using Shared.Interfaces;
using Client.Graphics;

namespace Client.Graphics
{
    public class ShaderLoader : IResourceLoader<ICSharpShader>
    {
        private readonly CSharpShaderManager _shaderManager;

        public ShaderLoader(CSharpShaderManager shaderManager)
        {
            _shaderManager = shaderManager;
        }

        public async Task<ICSharpShader?> LoadAsync(Stream stream, string path)
        {
            using var reader = new StreamReader(stream);
            string code = await reader.ReadToEndAsync();
            try
            {
                return await _shaderManager.CompileShaderAsync(code);
            }
            catch (Exception)
            {
                // Errors are logged in CSharpShaderManager
                return null;
            }
        }
    }
}
