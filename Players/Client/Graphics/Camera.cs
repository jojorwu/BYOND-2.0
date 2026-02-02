using Shared.Models;
using Shared.Interfaces;
using Shared.Enums;
using Shared.Operations;
using Shared.Services;
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

        public Matrix4x4 GetProjectionMatrix(float aspectRatio)
        {
            var width = 1280 * Zoom;
            var height = 720 * Zoom;
            return Matrix4x4.CreateOrthographicOffCenter(-width / 2, width / 2, height / 2, -height / 2, 0.1f, 100.0f);
        }
    }
}
