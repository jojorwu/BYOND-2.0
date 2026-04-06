using Robust.Shared.Maths;
using System.Numerics;

namespace Client.Graphics
{
    /// <summary>
    /// Represents a 2D orthographic camera for world viewing.
    /// Manages position, zoom, and matrix generation for the rendering pipeline.
    /// </summary>
    public class Camera
    {
        private Vector2 _position;
        private float _zoom;

        public Vector2 Position { get => _position; set => _position = value; }
        public float Zoom { get => _zoom; set => _zoom = value; }

        public Camera(Vector2 position, float zoom)
        {
            _position = position;
            _zoom = zoom;
        }

        /// <summary>
        /// Generates the view matrix based on camera position and zoom.
        /// </summary>
        public Matrix4x4 GetViewMatrix()
        {
            var translation = Matrix4x4.CreateTranslation(-_position.X, -_position.Y, 0);
            var scale = Matrix4x4.CreateScale(_zoom, _zoom, 1.0f);
            return translation * scale;
        }

        /// <summary>
        /// Generates an orthographic projection matrix for the given viewport dimensions.
        /// </summary>
        public Matrix4x4 GetProjectionMatrix(float width, float height)
        {
            var viewWidth = width * _zoom;
            var viewHeight = height * _zoom;
            return Matrix4x4.CreateOrthographicOffCenter(-viewWidth / 2, viewWidth / 2, viewHeight / 2, -viewHeight / 2, 0.1f, 100.0f);
        }
    }
}
