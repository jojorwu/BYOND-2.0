using Robust.Shared.Maths;
using System.Numerics;

namespace Client.Graphics
{
    public class Camera
    {
        public Vector2 Position { get; set; }
        public float Zoom { get; set; }

        public Camera(Vector2 position, float zoom)
        {
            Position = position;
            Zoom = zoom;
        }

        public Matrix4x4 GetViewMatrix()
        {
            var translation = Matrix4x4.CreateTranslation(-Position.X, -Position.Y, 0);
            var scale = Matrix4x4.CreateScale(Zoom, Zoom, 1.0f);
            return translation * scale;
        }

        public Matrix4x4 GetProjectionMatrix(float width, float height)
        {
            var viewWidth = width * Zoom;
            var viewHeight = height * Zoom;
            return Matrix4x4.CreateOrthographicOffCenter(-viewWidth / 2, viewWidth / 2, viewHeight / 2, -viewHeight / 2, 0.1f, 100.0f);
        }
    }
}
