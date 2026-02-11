using System.Numerics;
using Robust.Shared.Maths;
using Shared.Maths;

namespace Editor
{
    public static class Camera
    {
        public static Vector2 Position { get; set; } = Vector2.Zero;
        public static float Zoom { get; set; } = 1.0f;

        public static Matrix4x4 GetProjectionMatrix(float width, float height)
        {
            var ortho = Matrix4x4.CreateOrthographicOffCenter(0.0f, width, height, 0.0f, -1.0f, 1.0f);
            var transform = Matrix4x4.CreateTranslation(new Vector3(-Position.X, -Position.Y, 0)) *
                            Matrix4x4.CreateScale(Zoom, Zoom, 1.0f) *
                            Matrix4x4.CreateTranslation(width * 0.5f, height * 0.5f, 0);
            return transform * ortho;
        }

        public static Vector2d ScreenToWorld(Vector2 screenCoords, Matrix4x4 projectionMatrix)
        {
            Matrix4x4.Invert(projectionMatrix, out var invertedProjection);
            var worldCoords = Vector2.Transform(screenCoords, invertedProjection);
            return worldCoords.ToRobust();
        }

        public static Vector2i WorldToScreen(Vector2d worldCoords, Matrix4x4 projectionMatrix)
        {
            var screenCoords = Vector2.Transform(worldCoords.ToNumerics(), projectionMatrix);
            return new Vector2i((int)screenCoords.X, (int)screenCoords.Y);
        }
    }
}
