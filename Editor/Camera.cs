using System.Numerics;
using Robust.Shared.Maths;

namespace Editor
{
    public static class Camera
    {
        public static Matrix4x4 GetProjectionMatrix(float width, float height)
        {
            return Matrix4x4.CreateOrthographicOffCenter(0.0f, width, height, 0.0f, -1.0f, 1.0f);
        }

        public static Vector2 ScreenToWorld(Vector2 screenCoords, Matrix4x4 projectionMatrix)
        {
            Matrix4x4.Invert(projectionMatrix, out var invertedProjection);
            var worldCoords = Vector2.Transform(screenCoords, invertedProjection);
            return worldCoords;
        }

        public static Vector2i WorldToScreen(Vector2 worldCoords, Matrix4x4 projectionMatrix)
        {
            var screenCoords = Vector2.Transform(worldCoords, projectionMatrix);
            return new Vector2i((int)screenCoords.X, (int)screenCoords.Y);
        }
    }
}
