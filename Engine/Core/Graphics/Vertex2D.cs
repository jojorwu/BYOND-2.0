using Shared.Models;
using Shared.Interfaces;
using Shared.Enums;
using Shared.Operations;
using Shared.Services;
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
