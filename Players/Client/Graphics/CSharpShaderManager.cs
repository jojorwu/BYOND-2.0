using Microsoft.CodeAnalysis.CSharp.Scripting;
using Microsoft.CodeAnalysis.Scripting;
using Silk.NET.OpenGL;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace Client.Graphics
{
    public interface ICSharpShader
    {
        string GetVertexSource();
        string GetFragmentSource();
        void Setup(Shader shader);
        void Update(Shader shader, float deltaTime);
    }

    public class CSharpShaderManager
    {
        private readonly GL _gl;
        private readonly Dictionary<string, ICSharpShader> _compiledShaders = new();

        public CSharpShaderManager(GL gl)
        {
            _gl = gl;
        }

        public async Task<ICSharpShader> CompileShaderAsync(string code)
        {
            var options = ScriptOptions.Default
                .WithReferences(typeof(ICSharpShader).Assembly)
                .WithImports("System", "System.Numerics", "Client.Graphics");

            try
            {
                var script = CSharpScript.Create<ICSharpShader>(code, options);
                var result = await script.RunAsync();
                return result.ReturnValue;
            }
            catch (Exception e)
            {
                Console.WriteLine($"Error compiling C# shader: {e.Message}");
                throw;
            }
        }

        public Shader CreateGlShader(ICSharpShader csharpShader)
        {
            return new Shader(_gl, csharpShader.GetVertexSource(), csharpShader.GetFragmentSource());
        }
    }
}
