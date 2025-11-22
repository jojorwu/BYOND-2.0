using System;
using System.Diagnostics;
using System.Threading;

namespace Client
{
    public class LogicThread
    {
        public GameState PreviousState { get; private set; }
        public GameState CurrentState { get; private set; }

        private readonly object _lock = new object();
        private Thread _thread;
        private bool _isRunning;
        public const int TicksPerSecond = 30;
        public const float TimeStep = 1.0f / TicksPerSecond;
        private int _nextId = 0;

        public LogicThread()
        {
            PreviousState = new GameState();
            CurrentState = new GameState();
            _thread = new Thread(GameLoop);
        }

        public void Start()
        {
            _isRunning = true;
            _thread.Start();
        }

        public void Stop()
        {
            _isRunning = false;
            _thread.Join();
        }

        private void GameLoop()
        {
            var stopwatch = new Stopwatch();
            double accumulator = 0;

            // Initialize game state
            for (int y = 0; y < 10; y++)
            {
                for (int x = 0; x < 10; x++)
                {
                    var id = _nextId++;
                    CurrentState.Renderables.Add(id, new Core.Graphics.RenderableComponent
                    {
                        ID = id,
                        TextureID = 0,
                        Position = new System.Numerics.Vector2(x * 0.1f - 0.5f, y * 0.1f - 0.5f),
                        Rotation = 0,
                        Scale = new System.Numerics.Vector2(0.1f, 0.1f),
                        Color = new System.Numerics.Vector4((float)x / 10, (float)y / 10, 1.0f, 1.0f),
                        SourceRect = new System.Numerics.Vector4(0, 0, 1, 1)
                    });
                }
            }
            PreviousState = CurrentState.Clone();

            stopwatch.Start();
            double lastTime = stopwatch.Elapsed.TotalSeconds;

            while (_isRunning)
            {
                double currentTime = stopwatch.Elapsed.TotalSeconds;
                double frameTime = currentTime - lastTime;
                lastTime = currentTime;
                accumulator += frameTime;

                while (accumulator >= TimeStep)
                {
                    Update(TimeStep);
                    accumulator -= TimeStep;
                }
            }
        }

        private void Update(float deltaTime)
        {
            lock (_lock)
            {
                PreviousState = CurrentState.Clone();

                var newRenderables = new System.Collections.Generic.Dictionary<int, Core.Graphics.RenderableComponent>();
                foreach (var r in CurrentState.Renderables.Values)
                {
                    var newR = r;
                    newR.Position.X += 0.1f * deltaTime;
                    newR.Rotation += 0.5f * deltaTime;
                    if (newR.Position.X > 1.0f) newR.Position.X = -1.0f;
                    newRenderables[newR.ID] = newR;
                }
                CurrentState.Renderables = newRenderables;
                CurrentState.TickCount++;
            }
        }

        public (GameState, GameState) GetStatesForRender()
        {
            lock (_lock)
            {
                return (PreviousState, CurrentState);
            }
        }
    }
}
