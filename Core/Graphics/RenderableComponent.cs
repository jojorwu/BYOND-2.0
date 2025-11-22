using System.Numerics;

namespace Core.Graphics
{
    public struct RenderableComponent
    {
        public int ID;
        public int TextureID;
        public Vector2 Position;
        public float Rotation;
        public Vector2 Scale;
        public Vector4 Color;
        public Vector4 SourceRect;
    }
}
