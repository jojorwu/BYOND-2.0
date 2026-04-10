using Silk.NET.OpenGL;
using System.Numerics;
using Robust.Shared.Maths;
using Shared;

namespace Client.Graphics
{
    public class RenderContext
    {
        public GL GL { get; }
        public GameState? PreviousState { get; }
        public GameState CurrentState { get; }
        public float Alpha { get; }
        public Box2 CullRect { get; }
        public Matrix4x4 View { get; }
        public Matrix4x4 Projection { get; }
        public int Width { get; }
        public int Height { get; }
        public double DeltaTime { get; set; }

        public RenderContext(GL gl, GameState? previousState, GameState currentState, float alpha, Box2 cullRect, Matrix4x4 view, Matrix4x4 projection, int width, int height)
        {
            GL = gl;
            PreviousState = previousState;
            CurrentState = currentState;
            Alpha = alpha;
            CullRect = cullRect;
            View = view;
            Projection = projection;
            Width = width;
            Height = height;
        }
    }

    public interface IRenderPass
    {
        string Name { get; }
        void Execute(RenderContext context);
    }

    /// <summary>
    /// Orchestrates the execution of multiple render passes in a defined order.
    /// Manages the lifecycle of render passes that implement IDisposable.
    /// </summary>
    public class RenderPipeline : IDisposable
    {
        private readonly List<IRenderPass> _passes = new();

        /// <summary>
        /// Adds a render pass to the end of the pipeline.
        /// </summary>
        public void AddPass(IRenderPass pass) => _passes.Add(pass);

        /// <summary>
        /// Executes all registered render passes sequentially.
        /// </summary>
        public void Execute(RenderContext context)
        {
            foreach (var pass in _passes)
            {
                pass.Execute(context);
            }
        }

        public void Dispose()
        {
            foreach (var pass in _passes)
            {
                if (pass is IDisposable disposable)
                {
                    disposable.Dispose();
                }
            }
            _passes.Clear();
        }
    }
}
