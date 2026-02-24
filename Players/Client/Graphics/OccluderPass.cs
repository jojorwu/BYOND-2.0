using Shared.Enums;
using System.Numerics;
using Silk.NET.OpenGL;
using Shared;
using Robust.Shared.Maths;

namespace Client.Graphics
{
    public class OccluderPass : IRenderPass
    {
        private readonly OccluderMap _occluderMap;
        private readonly SpriteRenderer _spriteRenderer;

        public OccluderPass(OccluderMap occluderMap, SpriteRenderer spriteRenderer)
        {
            _occluderMap = occluderMap;
            _spriteRenderer = spriteRenderer;
        }

        public string Name => "Occluder";

        public void Execute(RenderContext context)
        {
            _occluderMap.Framebuffer.Resize(context.Width / 2, context.Height / 2);
            _occluderMap.Bind();
            context.GL.ClearColor(0, 0, 0, 1);
            context.GL.Clear(ClearBufferMask.ColorBufferBit);

            _spriteRenderer.Begin(context.View, context.Projection);
            foreach (var obj in context.CurrentState.GameObjects.Values)
            {
                var opacity = obj.GetVariable("opacity");
                if (opacity.Type == DreamValueType.Float && opacity.AsFloat() > 0)
                {
                    _spriteRenderer.DrawQuad(new Vector2(obj.X * 32, obj.Y * 32), new Vector2(32, 32), Color.White);
                }
            }
            _spriteRenderer.End();
            _occluderMap.Unbind();
        }
    }
}
