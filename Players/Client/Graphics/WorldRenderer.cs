using System;
using System.Collections.Generic;
using System.Linq;
using Silk.NET.OpenGL;
using Robust.Shared.Maths;
using System.Numerics;
using Shared;
using Client.Assets;

namespace Client.Graphics
{
    public class WorldRenderer : IDisposable
    {
        private readonly GL _gl;
        private readonly TextureCache _textureCache;
        private readonly DmiCache _dmiCache;
        private readonly IconCache _iconCache;

        private readonly Dictionary<Vector2i, RenderChunk> _chunks = new();
        private readonly HashSet<Vector2i> _visibleChunks = new();

        public WorldRenderer(GL gl, TextureCache textureCache, DmiCache dmiCache, IconCache iconCache)
        {
            _gl = gl;
            _textureCache = textureCache;
            _dmiCache = dmiCache;
            _iconCache = iconCache;
        }

        public void Render(GameState state, Box2 cullRect, SpriteRenderer spriteRenderer)
        {
            UpdateVisibleChunks(cullRect);

            foreach (var coords in _visibleChunks)
            {
                if (!_chunks.TryGetValue(coords, out var chunk))
                {
                    chunk = new RenderChunk(_gl, coords);
                    _chunks[coords] = chunk;
                }

                if (chunk.IsDirty)
                {
                    RebuildChunk(chunk, state);
                }

                // Chunks are drawn after static turfs are baked.
                // Wait, if I use SpriteRenderer for everything, Chunks might not be needed?
                // No, Chunks are "smart" because they avoid iterating all turfs.
                // But Chunks need to be drawn in correct layer order.

                // For BYOND, turfs are usually on layer 2.
                // We can have a "StaticTurf" pass.
            }
        }

        private void UpdateVisibleChunks(Box2 cullRect)
        {
            _visibleChunks.Clear();
            int minX = (int)Math.Floor(cullRect.Left / RenderChunk.ChunkSize);
            int maxX = (int)Math.Floor(cullRect.Right / RenderChunk.ChunkSize);
            int minY = (int)Math.Floor(cullRect.Top / RenderChunk.ChunkSize);
            int maxY = (int)Math.Floor(cullRect.Bottom / RenderChunk.ChunkSize);

            for (int x = minX; x <= maxX; x++)
            {
                for (int y = minY; y <= maxY; y++)
                {
                    _visibleChunks.Add(new Vector2i(x, y));
                }
            }
        }

        private void RebuildChunk(RenderChunk chunk, GameState state)
        {
            var vertices = new List<Vertex>();

            int startX = chunk.Coords.X * RenderChunk.ChunkSize;
            int startY = chunk.Coords.Y * RenderChunk.ChunkSize;
            int endX = startX + RenderChunk.ChunkSize;
            int endY = startY + RenderChunk.ChunkSize;

            // This is simplified. In a real scenario, we'd query the spatial grid for turfs in this range.
            foreach (var obj in state.GameObjects.Values)
            {
                if (obj.X >= startX && obj.X < endX && obj.Y >= startY && obj.Y < endY)
                {
                    // Bake static objects (turfs) into the chunk
                    // For now, let's just assume everything at layer 2 is a turf
                    // and doesn't move.

                    // (Logic to generate vertices for obj)
                }
            }

            chunk.Update(System.Runtime.InteropServices.CollectionsMarshal.AsSpan(vertices));
        }

        public void MarkAreaDirty(Box2i area)
        {
            int minX = area.Left / RenderChunk.ChunkSize;
            int maxX = area.Right / RenderChunk.ChunkSize;
            int minY = area.Top / RenderChunk.ChunkSize;
            int maxY = area.Bottom / RenderChunk.ChunkSize;

            for (int x = minX; x <= maxX; x++)
            {
                for (int y = minY; y <= maxY; y++)
                {
                    if (_chunks.TryGetValue(new Vector2i(x, y), out var chunk))
                    {
                        chunk.MarkDirty();
                    }
                }
            }
        }

        public void Dispose()
        {
            foreach (var chunk in _chunks.Values)
            {
                chunk.Dispose();
            }
            _chunks.Clear();
        }
    }
}
