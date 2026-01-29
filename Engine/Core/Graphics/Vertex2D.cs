using System.Numerics;
using System.Runtime.InteropServices;

namespace Core.Graphics
{
    [StructLayout(LayoutKind.Sequential)]
    public struct Vertex2D
    {
        public Vector2 Position; // 8 bytes
        public Vector2 UV;       // 8 bytes
        public Vector4 Color;    // 16 bytes
    }
}
