using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Shared.Config;
using Shared.Interfaces;
using Shared.Services;
using Silk.NET.Windowing;
using Silk.NET.Input;
using Silk.NET.OpenGL;
using Silk.NET.OpenGL.Extensions.ImGui;

namespace Editor;

/// <summary>
/// The main entry point for the Editor application lifecycle.
/// </summary>
public class EditorApplication : EngineApplication
{
    private IWindow? _window;
    private GL? _gl;
    private ImGuiController? _imGui;
    private readonly IConfigurationManager _config;
    private readonly IEditorUIService _uiService;

    public EditorApplication(
        ILogger<EditorApplication> logger,
        IEnumerable<IEngineService> services,
        IEnumerable<IEngineModule> modules,
        IEnumerable<ITickable> tickables,
        IEnumerable<IShrinkable> shrinkables,
        IEnumerable<IEngineLifecycle> lifecycles,
        IDiagnosticBus diagnosticBus,
        ILifecycleOrchestrator orchestrator,
        IConfigurationManager config,
        IEditorUIService uiService)
        : base(logger, services, modules, tickables, shrinkables, lifecycles, diagnosticBus)
    {
        _config = config;
        _uiService = uiService;
        SetOrchestrator(orchestrator);
    }

    protected override Task OnStartAsync(CancellationToken cancellationToken)
    {
        var options = WindowOptions.Default;
        options.Title = "BYOND 2.0 Editor";
        options.Size = new Silk.NET.Maths.Vector2D<int>(1280, 720);

        _window = Window.Create(options);
        _window.Load += OnLoad;
        _window.Update += OnUpdate;
        _window.Render += OnRender;

        // Window Run usually blocks, but EngineApplication lifecycle expected to be async.
        _ = Task.Run(() => _window.Run(), cancellationToken);

        return Task.CompletedTask;
    }

    private void OnLoad()
    {
        if (_window == null) return;

        _gl = GL.GetApi(_window);
        var input = _window.CreateInput();
        _imGui = new ImGuiController(_gl, _window, input);
        _uiService.Initialize(_gl, _window);
        _logger.LogInformation("Editor Window Loaded.");
    }

    private void OnUpdate(double dt)
    {
        _imGui?.Update((float)dt);
    }

    private void OnRender(double dt)
    {
        if (_gl == null) return;

        _gl.ClearColor(0.1f, 0.1f, 0.1f, 1.0f);
        _gl.Clear(ClearBufferMask.ColorBufferBit);

        _uiService.Render((float)dt);
        _imGui?.Render();
    }

    protected override Task OnStopAsync(CancellationToken cancellationToken)
    {
        _window?.Close();
        return Task.CompletedTask;
    }
}
