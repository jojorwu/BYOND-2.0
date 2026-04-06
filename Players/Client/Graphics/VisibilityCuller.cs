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

        private static void CalculateVisibilitySIMD(ReadOnlySpan<Vector2> positions, Box2 cullRect, Span<byte> visibilityMask)
        {
            int count = positions.Length;
            int vectorSize = Vector<float>.Count;
            int pointsPerVector = vectorSize / 2;

            // Efficiently initialize bounds vectors
            float[] lowData = new float[vectorSize];
            float[] highData = new float[vectorSize];
            for (int j = 0; j < vectorSize; j += 2)
            {
                lowData[j] = cullRect.Left;
                lowData[j + 1] = cullRect.Top;
                highData[j] = cullRect.Right;
                highData[j + 1] = cullRect.Bottom;
            }

            Vector<float> lowBounds = new Vector<float>(lowData);
            Vector<float> highBounds = new Vector<float>(highData);

            // Cast positions to float span for vector processing
            ReadOnlySpan<float> floatPositions = MemoryMarshal.Cast<Vector2, float>(positions);
            int i = 0;
            int pointIdx = 0;

            for (; i <= floatPositions.Length - vectorSize; i += vectorSize, pointIdx += pointsPerVector)
            {
                Vector<float> vPos = new Vector<float>(floatPositions.Slice(i));

                // Perform bounds check: (vPos >= low) AND (vPos <= high)
                var geLow = Vector.GreaterThanOrEqual(vPos, lowBounds);
                var leHigh = Vector.LessThanOrEqual(vPos, highBounds);
                var res = Vector.AsVectorInt32(geLow & leHigh);

                // Extract visibility for each point in the vector
                for (int j = 0; j < pointsPerVector; j++)
                {
                    // A point is visible if BOTH its X and Y components are within bounds.
                    // Vector comparison results in -1 (all bits set) for true, 0 for false.
                    visibilityMask[pointIdx + j] = (res[j * 2] != 0 && res[j * 2 + 1] != 0) ? (byte)1 : (byte)0;
                }
            }

            // Scalar remainder
            for (; pointIdx < count; pointIdx++)
            {
                Vector2 pos = positions[pointIdx];
                visibilityMask[pointIdx] = (pos.X >= cullRect.Left && pos.X <= cullRect.Right &&
                                           pos.Y >= cullRect.Top && pos.Y <= cullRect.Bottom) ? (byte)1 : (byte)0;
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
