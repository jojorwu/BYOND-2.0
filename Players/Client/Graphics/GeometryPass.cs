using Silk.NET.OpenGL;
using Shared;

namespace Client.Graphics
{
    public class GeometryPass : IRenderPass
    {
        private readonly GBuffer _gBuffer;
        private readonly WorldRenderer _worldRenderer;

        public GeometryPass(GBuffer gBuffer, WorldRenderer worldRenderer)
        {
            _gBuffer = gBuffer;
            _worldRenderer = worldRenderer;
        }

        public string Name => "Geometry";

        public void Execute(RenderContext context)
        {
            _gBuffer.Resize(context.Width, context.Height);
            _gBuffer.Bind();

            context.GL.ClearColor(0.0f, 0.0f, 0.0f, 1.0f);
            context.GL.Clear(ClearBufferMask.ColorBufferBit | ClearBufferMask.DepthBufferBit);

            _worldRenderer.Render(context.PreviousState, context.CurrentState, context.Alpha, context.CullRect, context.View, context.Projection);

            _gBuffer.Unbind();
        }
    }
}
