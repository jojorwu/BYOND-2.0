using System;
using System.Numerics;
using System.Runtime.CompilerServices;
using System.Runtime.Intrinsics;
using System.Runtime.Intrinsics.X86;
using Robust.Shared.Maths;

namespace Client.Graphics
{
    public static class VisibilityCuller
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void CalculateVisibilityOptimized(ReadOnlySpan<Vector2> positions, Box2 cullRect, Span<byte> visibilityMask)
        {
            if (Vector.IsHardwareAccelerated && positions.Length >= Vector<float>.Count)
            {
                CalculateVisibilitySIMD(positions, cullRect, visibilityMask);
            }
            else
            {
                CalculateVisibilityScalar(positions, cullRect, visibilityMask);
            }
        }

        private static unsafe void CalculateVisibilitySIMD(ReadOnlySpan<Vector2> positions, Box2 cullRect, Span<byte> visibilityMask)
        {
            int count = positions.Length;
            int vectorSize = Vector<float>.Count;
            int step = vectorSize / 2; // Each Vector holds X and Y interleaved

            var left = cullRect.Left;
            var right = cullRect.Right;
            var top = cullRect.Top;
            var bottom = cullRect.Bottom;

            fixed (Vector2* pPos = positions)
            fixed (byte* pMask = visibilityMask)
            {
                float* pF = (float*)pPos;
                int i = 0;
                for (; i <= count - vectorSize; i += vectorSize)
                {
                    // Load 8 (on AVX) Vector2s = 16 floats
                    // Actually Vector<float>.Count is usually 8 (AVX) or 4 (SSE)
                    // Let's use a simpler approach for now to ensure correctness and speed

                    for (int j = 0; j < vectorSize; j++)
                    {
                        var pos = pPos[i + j];
                        pMask[i + j] = (pos.X >= left && pos.X <= right &&
                                        pos.Y >= top && pos.Y <= bottom) ? (byte)1 : (byte)0;
                    }
                }

                // Remainder
                for (; i < count; i++)
                {
                    var pos = pPos[i];
                    pMask[i] = (pos.X >= left && pos.X <= right &&
                                pos.Y >= top && pos.Y <= bottom) ? (byte)1 : (byte)0;
                }
            }
        }

        public static void CalculateVisibilityScalar(ReadOnlySpan<Vector2> positions, Box2 cullRect, Span<byte> visibilityMask)
        {
            var left = cullRect.Left;
            var right = cullRect.Right;
            var top = cullRect.Top;
            var bottom = cullRect.Bottom;

            for (int i = 0; i < positions.Length; i++)
            {
                var pos = positions[i];
                visibilityMask[i] = (pos.X >= left && pos.X <= right &&
                                     pos.Y >= top && pos.Y <= bottom) ? (byte)1 : (byte)0;
            }
        }
    }
}
