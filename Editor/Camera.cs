using System.Numerics;

namespace Editor
{
    public class Camera
    {
        private float _width;
        private float _height;

        public void Update(float width, float height)
        {
            _width = width;
            _height = height;
        }

        public Matrix4x4 GetProjectionMatrix()
        {
            return Matrix4x4.CreateOrthographicOffCenter(0.0f, _width, _height, 0.0f, -1.0f, 1.0f);
        }
    }
}
