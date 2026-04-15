using Microsoft.CodeAnalysis.CSharp.Scripting;
using Microsoft.CodeAnalysis.Scripting;
using Silk.NET.OpenGL;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Shared.Services;
using Shared.Interfaces;

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
    private readonly IResourceSystem _resourceSystem;
        private GL? _gl;
        private readonly Dictionary<string, ICSharpShader> _compiledShaders = new();
    private readonly Dictionary<ICSharpShader, Shader> _glShaders = new();

    public CSharpShaderManager(ILogger<CSharpShaderManager> logger, IResourceSystem resourceSystem)
        {
            _logger = logger;
        _resourceSystem = resourceSystem;
    }

    protected override Task OnInitializeAsync()
    {
        _resourceSystem.RegisterLoader(new ShaderLoader(this));
        _resourceSystem.ResourceReloaded += OnResourceReloaded;
        return Task.CompletedTask;
    }

    private async void OnResourceReloaded(string path)
    {
        if (path.EndsWith(".cs", StringComparison.OrdinalIgnoreCase))
        {
            _logger.LogInformation("Reloading shader: {Path}", path);
            await _resourceSystem.LoadResourceAsync<ICSharpShader>(path);
        }
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

        if (_glShaders.TryGetValue(csharpShader, out var shader))
        {
            return shader;
        }

        shader = new Shader(_gl, csharpShader.GetVertexSource(), csharpShader.GetFragmentSource());
        _glShaders[csharpShader] = shader;
        return shader;
    }

    public override void Dispose()
    {
        foreach (var shader in _glShaders.Values)
        {
            shader.Dispose();
        }
        _glShaders.Clear();
        base.Dispose();
        }
    }
}
