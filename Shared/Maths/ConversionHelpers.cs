using System.Numerics;
using Robust.Shared.Maths;

namespace Shared.Maths
{
    public static class ConversionHelpers
    {
        public static Vector2 ToNumerics(this Vector2d vec)
        {
            return new Vector2((float)vec.X, (float)vec.Y);
        }

        public static Vector2d ToRobust(this Vector2 vec)
        {
            return new Vector2d(vec.X, vec.Y);
        }
    }
}
