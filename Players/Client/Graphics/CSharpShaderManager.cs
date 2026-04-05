using Microsoft.CodeAnalysis.CSharp.Scripting;
using Microsoft.CodeAnalysis.Scripting;
using Silk.NET.OpenGL;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Shared.Services;

namespace Client.Graphics
{
    public interface ICSharpShader
    {
        string GetVertexSource();
        string GetFragmentSource();
        void Setup(Shader shader);
        void Update(Shader shader, float deltaTime);
    }

    public class CSharpShaderManager : EngineService
    {
        private readonly ILogger<CSharpShaderManager> _logger;
        private GL? _gl;
        private readonly Dictionary<string, ICSharpShader> _compiledShaders = new();

        public CSharpShaderManager(ILogger<CSharpShaderManager> logger)
        {
            _logger = logger;
        }

        public void SetGL(GL gl)
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
                _logger.LogError(e, "Error compiling C# shader");
                throw;
            }
        }

        public Shader CreateGlShader(ICSharpShader csharpShader)
        {
            if (_gl == null) throw new InvalidOperationException("CSharpShaderManager not initialized with GL context.");
            return new Shader(_gl, csharpShader.GetVertexSource(), csharpShader.GetFragmentSource());
        }
    }
}
