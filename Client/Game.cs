using Silk.NET.Windowing;
using Silk.NET.Input;
using Silk.NET.OpenGL;
using System;
using Core.Graphics;
using System.Numerics;
using System.Diagnostics;

namespace Client
{
    public class Game
    {
        private IWindow window;
        private GL? Gl;
        private BatchRenderer? batchRenderer;
        private uint whiteTexture;
        private LogicThread? logicThread;
        private Stopwatch? gameTimer;
        private double lastLogicTickTime;
        private long lastTickCount = -1;

        public Game()
        {
            var options = WindowOptions.Default;
            options.Title = "BYOND 2.0";
            options.Size = new Silk.NET.Maths.Vector2D<int>(1280, 720);
            window = Window.Create(options);

            window.Load += OnLoad;
            window.Update += OnUpdate;
            window.Render += OnRender;
            window.Closing += OnClose;
        }

        public void Run()
        {
            window.Run();
        }

        private unsafe void OnLoad()
        {
            IInputContext input = window.CreateInput();
            for (int i = 0; i < input.Keyboards.Count; i++)
            {
                input.Keyboards[i].KeyDown += KeyDown;
            }

            Gl = GL.GetApi(window);
            batchRenderer = new BatchRenderer(Gl);

            whiteTexture = Gl.GenTexture();
            Gl.BindTexture(TextureTarget.Texture2D, whiteTexture);
            byte[] whitePixel = { 255, 255, 255, 255 };
            fixed (void* p = whitePixel)
            {
                Gl.TexImage2D(TextureTarget.Texture2D, 0, InternalFormat.Rgba, 1, 1, 0, PixelFormat.Rgba, PixelType.UnsignedByte, p);
            }
            Gl.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureMinFilter, (int)TextureMinFilter.Linear);
            Gl.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureMagFilter, (int)TextureMagFilter.Linear);

            gameTimer = new Stopwatch();
            gameTimer.Start();
            lastLogicTickTime = gameTimer.Elapsed.TotalSeconds;

            logicThread = new LogicThread();
            logicThread.Start();
        }

        private void OnUpdate(double deltaTime)
        {
        }

        private void OnRender(double deltaTime)
        {
            Gl!.ClearColor(0.2f, 0.3f, 0.3f, 1.0f);
            Gl!.Clear(ClearBufferMask.ColorBufferBit);

            var projection = Matrix4x4.CreateOrthographicOffCenter(-1.0f, 1.0f, -1.0f, 1.0f, -1.0f, 1.0f);
            var view = Matrix4x4.Identity;
            batchRenderer!.SetCamera(view, projection);

            var (prevState, currState) = logicThread!.GetStatesForRender();

            if (currState.TickCount > lastTickCount)
            {
                lastLogicTickTime = gameTimer!.Elapsed.TotalSeconds;
                lastTickCount = currState.TickCount;
            }

            double now = gameTimer!.Elapsed.TotalSeconds;
            float alpha = (float)((now - lastLogicTickTime) / LogicThread.TimeStep);

            batchRenderer.Begin((int)whiteTexture);
            foreach (var curr in currState.Renderables.Values)
            {
                if (prevState.Renderables.TryGetValue(curr.ID, out var prev))
                {
                    var interpolatedPos = Vector2.Lerp(prev.Position, curr.Position, alpha);
                    var interpolatedRot = prev.Rotation + (curr.Rotation - prev.Rotation) * alpha;

                    var component = new RenderableComponent
                    {
                        TextureID = (int)whiteTexture,
                        Position = interpolatedPos,
                        Rotation = interpolatedRot,
                        Scale = curr.Scale,
                        Color = curr.Color,
                        SourceRect = curr.SourceRect
                    };
                    batchRenderer.Draw(component);
                }
            }
            batchRenderer.End();
        }

        private void OnClose()
        {
            logicThread!.Stop();
            batchRenderer!.Dispose();
        }

        private void KeyDown(IKeyboard keyboard, Key key, int arg3)
        {
            if (key == Key.Escape)
            {
                window.Close();
            }
        }
    }
}
