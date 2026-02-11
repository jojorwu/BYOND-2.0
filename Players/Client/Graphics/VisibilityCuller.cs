using System;
using System.Numerics;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
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
            int pointsPerVector = vectorSize / 2;

            // Pre-calculate interleaved bounds on the stack
            float* pLow = stackalloc float[vectorSize];
            float* pHigh = stackalloc float[vectorSize];
            for (int j = 0; j < vectorSize; j += 2)
            {
                pLow[j] = cullRect.Left;
                pLow[j + 1] = cullRect.Top;
                pHigh[j] = cullRect.Right;
                pHigh[j + 1] = cullRect.Bottom;
            }

            Vector<float> lowBounds = Unsafe.Read<Vector<float>>(pLow);
            Vector<float> highBounds = Unsafe.Read<Vector<float>>(pHigh);

            fixed (Vector2* pPos = positions)
            fixed (byte* pMask = visibilityMask)
            {
                float* pF = (float*)pPos;
                int i = 0;
                // Process in steps of pointsPerVector
                for (; i <= count - pointsPerVector; i += pointsPerVector)
                {
                    Vector<float> vPos = Unsafe.Read<Vector<float>>(pF + i * 2);

                    // (Pos >= Low) & (Pos <= High)
                    // Resulting Vector<int> has all 1s bits (-1) where true, all 0s where false
                    Vector<int> res = Vector.GreaterThanOrEqual(vPos, lowBounds) & Vector.LessThanOrEqual(vPos, highBounds);

                    for (int j = 0; j < pointsPerVector; j++)
                    {
                        // Each point has two floats (X and Y)
                        // A point is visible if BOTH its floats passed the test

                        int xRes = res[j * 2];
                        int yRes = res[j * 2 + 1];

                        // xRes and yRes will be -1 (all bits 1) if true
                        pMask[i + j] = (xRes != 0 && yRes != 0) ? (byte)1 : (byte)0;
                    }
                }

                // Remainder
                for (; i < count; i++)
                {
                    Vector2 pos = pPos[i];
                    pMask[i] = (pos.X >= cullRect.Left && pos.X <= cullRect.Right &&
                                 pos.Y >= cullRect.Top && pos.Y <= cullRect.Bottom) ? (byte)1 : (byte)0;
                }
            }
        }

        public static void CalculateVisibilityScalar(ReadOnlySpan<Vector2> positions, Box2 cullRect, Span<byte> visibilityMask)
        {
            float left = cullRect.Left;
            float right = cullRect.Right;
            float top = cullRect.Top;
            float bottom = cullRect.Bottom;

            for (int i = 0; i < positions.Length; i++)
            {
                Vector2 pos = positions[i];
                visibilityMask[i] = (pos.X >= left && pos.X <= right &&
                                     pos.Y >= top && pos.Y <= bottom) ? (byte)1 : (byte)0;
            }
        }
    }
}
