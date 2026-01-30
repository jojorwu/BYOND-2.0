using Robust.Shared.Maths;

namespace Core.Graphics
{
    public struct RenderableComponent
    {
        public int ID;
        public int TextureID;
        public Vector2d Position;
        public float Rotation;
        public Vector2d Scale;
        public Vector4d Color;
        public Vector4d SourceRect;
    }
}
