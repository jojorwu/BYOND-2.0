using System;
using System.Numerics;
using System.Runtime.InteropServices;
using Core.Graphics;
using Silk.NET.OpenGL;
using System.IO;

namespace Client
{
    public class BatchRenderer : IDisposable
    {
        private const int MaxSprites = 10000;
        private Vertex2D[] vertices;
        private uint[] indices;
        private int spriteCount;
        private int currentTexture;
        private GL Gl;
        private uint Vao;
        private uint Vbo;
        private uint Ebo;
        private uint ShaderProgram;

        public unsafe BatchRenderer(GL gl)
        {
            Gl = gl;
            vertices = new Vertex2D[MaxSprites * 4];
            indices = new uint[MaxSprites * 6];

            for (uint i = 0, offset = 0; i < MaxSprites * 6; i += 6, offset += 4)
            {
                indices[i + 0] = offset + 0;
                indices[i + 1] = offset + 1;
                indices[i + 2] = offset + 2;
                indices[i + 3] = offset + 2;
                indices[i + 4] = offset + 3;
                indices[i + 5] = offset + 0;
            }

            Vao = Gl.GenVertexArray();
            Gl.BindVertexArray(Vao);

            Vbo = Gl.GenBuffer();
            Gl.BindBuffer(BufferTargetARB.ArrayBuffer, Vbo);
            Gl.BufferData(BufferTargetARB.ArrayBuffer, (nuint)(MaxSprites * 4 * sizeof(Vertex2D)), null, BufferUsageARB.DynamicDraw);

            Ebo = Gl.GenBuffer();
            Gl.BindBuffer(BufferTargetARB.ElementArrayBuffer, Ebo);
            fixed(void* i = &indices[0])
            {
                Gl.BufferData(BufferTargetARB.ElementArrayBuffer, (nuint)(MaxSprites * 6 * sizeof(uint)), i, BufferUsageARB.StaticDraw);
            }

            // Shaders
            string vertexShaderSource = File.ReadAllText("shader.vert");
            string fragmentShaderSource = File.ReadAllText("shader.frag");

            uint vertexShader = Gl.CreateShader(ShaderType.VertexShader);
            Gl.ShaderSource(vertexShader, vertexShaderSource);
            Gl.CompileShader(vertexShader);
            Gl.GetShader(vertexShader, ShaderParameterName.CompileStatus, out int vStatus);
            if (vStatus != (int)GLEnum.True)
            {
                throw new Exception("Vertex shader failed to compile: " + Gl.GetShaderInfoLog(vertexShader));
            }

            uint fragmentShader = Gl.CreateShader(ShaderType.FragmentShader);
            Gl.ShaderSource(fragmentShader, fragmentShaderSource);
            Gl.CompileShader(fragmentShader);
            Gl.GetShader(fragmentShader, ShaderParameterName.CompileStatus, out int fStatus);
            if (fStatus != (int)GLEnum.True)
            {
                throw new Exception("Fragment shader failed to compile: " + Gl.GetShaderInfoLog(fragmentShader));
            }

            ShaderProgram = Gl.CreateProgram();
            Gl.AttachShader(ShaderProgram, vertexShader);
            Gl.AttachShader(ShaderProgram, fragmentShader);
            Gl.LinkProgram(ShaderProgram);
            Gl.GetProgram(ShaderProgram, GLEnum.LinkStatus, out int pStatus);
            if (pStatus != (int)GLEnum.True)
            {
                throw new Exception("Shader program failed to link: " + Gl.GetProgramInfoLog(ShaderProgram));
            }

            Gl.DetachShader(ShaderProgram, vertexShader);
            Gl.DetachShader(ShaderProgram, fragmentShader);
            Gl.DeleteShader(vertexShader);
            Gl.DeleteShader(fragmentShader);

            Gl.EnableVertexAttribArray(0);
            Gl.VertexAttribPointer(0, 2, VertexAttribPointerType.Float, false, (uint)sizeof(Vertex2D), (void*)0);

            Gl.EnableVertexAttribArray(1);
            Gl.VertexAttribPointer(1, 2, VertexAttribPointerType.Float, false, (uint)sizeof(Vertex2D), (void*)Marshal.OffsetOf<Vertex2D>(nameof(Vertex2D.UV)));

            Gl.EnableVertexAttribArray(2);
            Gl.VertexAttribPointer(2, 4, VertexAttribPointerType.Float, false, (uint)sizeof(Vertex2D), (void*)Marshal.OffsetOf<Vertex2D>(nameof(Vertex2D.Color)));
        }

        public void Begin(int textureId)
        {
            currentTexture = textureId;
            spriteCount = 0;
        }

        public void Draw(RenderableComponent component)
        {
            if (spriteCount >= MaxSprites || (component.TextureID != currentTexture && spriteCount > 0))
            {
                Flush();
                Begin(component.TextureID);
            }

            float cos = (float)Math.Cos(component.Rotation);
            float sin = (float)Math.Sin(component.Rotation);

            Vector2[] quadVertices =
            {
                new Vector2(-0.5f * component.Scale.X, -0.5f * component.Scale.Y),
                new Vector2(0.5f * component.Scale.X, -0.5f * component.Scale.Y),
                new Vector2(0.5f * component.Scale.X, 0.5f * component.Scale.Y),
                new Vector2(-0.5f * component.Scale.X, 0.5f * component.Scale.Y)
            };

            for (int i = 0; i < 4; i++)
            {
                quadVertices[i] = new Vector2(
                    quadVertices[i].X * cos - quadVertices[i].Y * sin + component.Position.X,
                    quadVertices[i].X * sin + quadVertices[i].Y * cos + component.Position.Y
                );
            }

            Vector2[] uvs =
            {
                new Vector2(component.SourceRect.X, component.SourceRect.Y),
                new Vector2(component.SourceRect.X + component.SourceRect.Z, component.SourceRect.Y),
                new Vector2(component.SourceRect.X + component.SourceRect.Z, component.SourceRect.Y + component.SourceRect.W),
                new Vector2(component.SourceRect.X, component.SourceRect.Y + component.SourceRect.W)
            };

            vertices[spriteCount * 4 + 0] = new Vertex2D { Position = quadVertices[0], UV = uvs[0], Color = component.Color };
            vertices[spriteCount * 4 + 1] = new Vertex2D { Position = quadVertices[1], UV = uvs[1], Color = component.Color };
            vertices[spriteCount * 4 + 2] = new Vertex2D { Position = quadVertices[2], UV = uvs[2], Color = component.Color };
            vertices[spriteCount * 4 + 3] = new Vertex2D { Position = quadVertices[3], UV = uvs[3], Color = component.Color };

            spriteCount++;
        }

        public void End()
        {
            Flush();
        }

        public void SetCamera(Matrix4x4 view, Matrix4x4 projection)
        {
            Gl.UseProgram(ShaderProgram);
            int viewLoc = Gl.GetUniformLocation(ShaderProgram, "uView");
            int projLoc = Gl.GetUniformLocation(ShaderProgram, "uProjection");
            Gl.UniformMatrix4(viewLoc, 1, false, in view.M11);
            Gl.UniformMatrix4(projLoc, 1, false, in projection.M11);
        }

        private unsafe void Flush()
        {
            if (spriteCount == 0) return;

            Gl.BindVertexArray(Vao);
            Gl.UseProgram(ShaderProgram);

            Gl.ActiveTexture(TextureUnit.Texture0);
            Gl.BindTexture(TextureTarget.Texture2D, (uint)currentTexture);

            Gl.BindBuffer(BufferTargetARB.ArrayBuffer, Vbo);
            fixed(void* v = &vertices[0])
            {
                Gl.BufferSubData(BufferTargetARB.ArrayBuffer, 0, (nuint)(spriteCount * 4 * sizeof(Vertex2D)), v);
            }

            Gl.DrawElements(PrimitiveType.Triangles, (uint)(spriteCount * 6), DrawElementsType.UnsignedInt, null);

            spriteCount = 0;
        }

        public void Dispose()
        {
            Gl.DeleteVertexArray(Vao);
            Gl.DeleteBuffer(Vbo);
            Gl.DeleteBuffer(Ebo);
            Gl.DeleteProgram(ShaderProgram);
        }
    }
}
