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

            var archetypes = context.CurrentState.ArchetypeManager.GetArchetypesWithComponents(ReadOnlySpan<Type>.Empty);
            foreach (var arch in archetypes)
            {
                int count = arch.EntityCount;
                if (count == 0) continue;

                for (int i = 0; i < count; i++)
                {
                    if (arch.GetOpacity(i) > 0)
                    {
                        _spriteRenderer.DrawQuad(new Vector2(arch.GetX(i) * 32, arch.GetY(i) * 32), new Vector2(32, 32), Color.White);
                    }
                }
            }

            _spriteRenderer.End();
            _occluderMap.Unbind();
        }
    }
}
