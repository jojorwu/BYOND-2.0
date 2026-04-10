using Shared;
using Shared.Config;
using Shared.Attributes;
using Shared.Interfaces;
using Shared.Services;
using Microsoft.Extensions.Logging;
using Silk.NET.Windowing;
using Silk.NET.Input;
using Silk.NET.OpenGL;
using System.Threading;
using System.Threading.Tasks;

namespace Editor
{
    public class EditorApplication : EngineApplication
    {
        private IWindow _window = null!;
        private GL _gl = null!;
        private readonly IConfigurationManager _config;

        public EditorApplication(
            ILogger<EditorApplication> logger,
            IEnumerable<IEngineService> services,
            IEnumerable<IEngineModule> modules,
            IEnumerable<ITickable> tickables,
            IEnumerable<IShrinkable> shrinkables,
            IEnumerable<IEngineLifecycle> lifecycles,
            IDiagnosticBus diagnosticBus,
            ILifecycleOrchestrator orchestrator,
            IConfigurationManager config)
            : base(logger, services, modules, tickables, shrinkables, lifecycles, diagnosticBus)
        {
            _config = config;
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

            // Run on a separate thread to not block the DI startup if needed,
            // but usually we want to block here until the window closes.
            _ = Task.Run(() => _window.Run(), cancellationToken);

            return Task.CompletedTask;
        }

        private void OnLoad()
        {
            _gl = GL.GetApi(_window);
            _logger.LogInformation("Editor Window Loaded.");
        }

        private void OnUpdate(double dt)
        {
            // Update logic moved to TickAsync
        }

        private void OnRender(double dt)
        {
            _gl.ClearColor(0.1f, 0.1f, 0.1f, 1.0f);
            _gl.Clear(ClearBufferMask.ColorBufferBit);
        }

        protected override Task OnStopAsync(CancellationToken cancellationToken)
        {
            _window?.Close();
            return Task.CompletedTask;
        }
    }
}
