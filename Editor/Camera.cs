using Silk.NET.Maths;
using System.Numerics;

namespace Editor
{
    public class Camera
    {
        public Vector2 Position { get; set; } = Vector2.Zero;
        public float Zoom { get; set; } = 1.0f;

        public Matrix4x4 GetViewMatrix()
        {
            var translation = Matrix4x4.CreateTranslation(-Position.X, -Position.Y, 0);
            var scale = Matrix4x4.CreateScale(Zoom, Zoom, 1.0f);
            return translation * scale;
        }

        public void AdjustZoom(float delta)
        {
            Zoom += delta * 0.1f;
            if (Zoom < 0.1f) Zoom = 0.1f;
            if (Zoom > 10.0f) Zoom = 10.0f;
        }

        public void Move(Vector2 delta)
        {
            Position += delta / Zoom;
        }
    }
}
