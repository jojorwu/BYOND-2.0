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

        public Vector2 WorldToScreen(Vector2 worldPos, float width, float height)
        {
            var view = GetViewMatrix();
            var projection = GetProjectionMatrix(width, height);
            var viewProj = view * projection;
            var clipSpace = Vector4.Transform(new Vector4(worldPos, 0, 1), viewProj);
            return new Vector2(
                (clipSpace.X + 1.0f) * 0.5f * width,
                (1.0f - clipSpace.Y) * 0.5f * height
            );
        }

        public Vector2 ScreenToWorld(Vector2 screenPos, float width, float height)
        {
            var view = GetViewMatrix();
            var projection = GetProjectionMatrix(width, height);
            Matrix4x4.Invert(view * projection, out var invViewProj);
            var clipSpace = new Vector4(
                (screenPos.X / width) * 2.0f - 1.0f,
                1.0f - (screenPos.Y / height) * 2.0f,
                0.0f, 1.0f
            );
            var worldSpace = Vector4.Transform(clipSpace, invViewProj);
            return new Vector2(worldSpace.X, worldSpace.Y) / worldSpace.W;
        }
    }
}
