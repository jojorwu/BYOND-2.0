using System.Numerics;
using Silk.NET.OpenGL;
using Shared;
using Robust.Shared.Maths;

namespace Client.Graphics
{
    public class LightingPass : IRenderPass
    {
        private readonly LightingRenderer _lightingRenderer;
        private readonly GBuffer _gBuffer;
        private readonly OccluderMap _occluderMap;
        private readonly Framebuffer _sceneFramebuffer;
        private readonly ParticleSystem _particleSystem;

        public LightingPass(LightingRenderer lightingRenderer, GBuffer gBuffer, OccluderMap occluderMap, Framebuffer sceneFramebuffer, ParticleSystem particleSystem)
        {
            _lightingRenderer = lightingRenderer;
            _gBuffer = gBuffer;
            _occluderMap = occluderMap;
            _sceneFramebuffer = sceneFramebuffer;
            _particleSystem = particleSystem;
        }

        public string Name => "Lighting";

        public void Execute(RenderContext context)
        {
            _sceneFramebuffer.Resize(context.Width, context.Height);

            // Copy Albedo from G-Buffer to Scene
            context.GL.BindFramebuffer(FramebufferTarget.ReadFramebuffer, _gBuffer.Fbo);
            context.GL.BindFramebuffer(FramebufferTarget.DrawFramebuffer, _sceneFramebuffer.Fbo);
            context.GL.ReadBuffer(ReadBufferMode.ColorAttachment0);
            context.GL.BlitFramebuffer(0, 0, _gBuffer.Width, _gBuffer.Height, 0, 0, _sceneFramebuffer.Width, _sceneFramebuffer.Height, ClearBufferMask.ColorBufferBit, BlitFramebufferFilter.Nearest);

            _sceneFramebuffer.Bind();

            // Note: Depth testing for 3D objects was here in Game.cs, but we'll focus on 2D lighting for now.

            foreach (var obj in context.CurrentState.GameObjects.Values)
            {
                var lightPower = obj.GetVariable("light_power");
                if (lightPower.Type == DreamValueType.Float && lightPower.AsFloat() > 0)
                {
                    _lightingRenderer.AddLight(new Vector2(obj.X * 32 + 16, obj.Y * 32 + 16), lightPower.AsFloat() * 32, Color.White);

                    if (System.Random.Shared.Next(0, 10) == 0) {
                        _particleSystem.Emit(new Vector2(obj.X * 32 + 16, obj.Y * 32 + 16), new Vector2((float)System.Random.Shared.NextDouble() * 20 - 10, (float)System.Random.Shared.NextDouble() * 20 - 10), Color.Yellow, 1.0f);
                    }
                }
            }

            var worldBounds = new Box2(context.CullRect.Left * 32, context.CullRect.Top * 32, context.CullRect.Right * 32, context.CullRect.Bottom * 32);
            _lightingRenderer.Render(context.View, context.Projection, _gBuffer.NormalTexture, _occluderMap.Texture, worldBounds);

            _particleSystem.Render(context.View, context.Projection);

            _sceneFramebuffer.Unbind();
        }
    }
}
