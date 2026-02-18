using Silk.NET.OpenGL;
using System.Numerics;
using Robust.Shared.Maths;

namespace Client.Graphics
{
    public class PostProcessPass : IRenderPass
    {
        private readonly SSAOShader _ssaoShader;
        private readonly BloomShader _bloomShader;
        private readonly PostProcessShader _postProcessShader;
        private readonly OccluderMap _occluderMap;
        private readonly Framebuffer _sceneFramebuffer;
        private readonly Framebuffer[] _bloomBuffers;
        private readonly SpriteRenderer _spriteRenderer;

        public PostProcessPass(SSAOShader ssaoShader, BloomShader bloomShader, PostProcessShader postProcessShader, OccluderMap occluderMap, Framebuffer sceneFramebuffer, Framebuffer[] bloomBuffers, SpriteRenderer spriteRenderer)
        {
            _ssaoShader = ssaoShader;
            _bloomShader = bloomShader;
            _postProcessShader = postProcessShader;
            _occluderMap = occluderMap;
            _sceneFramebuffer = sceneFramebuffer;
            _bloomBuffers = bloomBuffers;
            _spriteRenderer = spriteRenderer;
        }

        public string Name => "PostProcess";

        public void Execute(RenderContext context)
        {
            // SSAO
            _ssaoShader.Use(_occluderMap.Texture);

            // Bloom
            _bloomBuffers[0].Resize(context.Width / 2, context.Height / 2);
            _bloomBuffers[1].Resize(context.Width / 2, context.Height / 2);

            _bloomBuffers[0].Bind();
            _bloomShader.ExtractBright(_sceneFramebuffer.Texture);
            DrawSimpleQuad(context.GL);
            _bloomBuffers[0].Unbind();

            bool horizontal = true;
            for (int i = 0; i < 4; i++)
            {
                _bloomBuffers[horizontal ? 1 : 0].Bind();
                _bloomShader.Blur(_bloomBuffers[horizontal ? 0 : 1].Texture, horizontal);
                DrawSimpleQuad(context.GL);
                horizontal = !horizontal;
            }

            // Final Composite
            context.GL.Clear(ClearBufferMask.ColorBufferBit);

            _postProcessShader.Use(_sceneFramebuffer.Texture);
            context.GL.Enable(EnableCap.Blend);
            context.GL.BlendFunc(BlendingFactor.One, BlendingFactor.One);

            DrawSimpleQuad(context.GL);

            context.GL.BindTexture(TextureTarget.Texture2D, _bloomBuffers[horizontal ? 0 : 1].Texture);
            DrawSimpleQuad(context.GL);

            context.GL.Disable(EnableCap.Blend);
        }

        private void DrawSimpleQuad(GL gl)
        {
            _spriteRenderer.Begin(Matrix4x4.Identity, Matrix4x4.CreateOrthographicOffCenter(-1, 1, -1, 1, -1, 1));
            _spriteRenderer.Draw(0, new Box2(0, 0, 1, 1), new Vector2(-1, -1), new Vector2(2, 2), Color.White);
            _spriteRenderer.End();
        }
    }
}
