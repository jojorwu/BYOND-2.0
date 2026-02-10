using System;
using System.Collections.Generic;
using System.Linq;
using Silk.NET.OpenGL;
using Robust.Shared.Maths;
using System.Numerics;
using Shared;
using Core.Dmi;
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

        public void Render(GameState state, Box2 cullRect)
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

                chunk.Draw();
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

            foreach (var obj in state.GameObjects.Values)
            {
                // Only bake static turfs (layer 2) into chunks
                if (obj.X >= startX && obj.X < endX && obj.Y >= startY && obj.Y < endY)
                {
                    var layer = GetLayer(obj);
                    if (layer == 2.0f) // Typical turf layer
                    {
                        var icon = GetIcon(obj);
                        if (!string.IsNullOrEmpty(icon))
                        {
                            var (dmiPath, stateName) = _iconCache.ParseIconString(icon);
                            var texture = _textureCache.GetTexture(dmiPath.Replace(".dmi", ".png"));
                            var dmi = _dmiCache.GetDmi(dmiPath, texture);
                            if (dmi != null)
                            {
                                var dmiState = dmi.Description.GetStateOrDefault(stateName);
                                if (dmiState != null)
                                {
                                    var frame = dmiState.GetFrames(AtomDirection.South)[0];
                                    var uv = new Box2(
                                        (float)frame.X / dmi.Width,
                                        (float)frame.Y / dmi.Height,
                                        (float)(frame.X + dmi.Description.Width) / dmi.Width,
                                        (float)(frame.Y + dmi.Description.Height) / dmi.Height
                                    );

                                    AddQuad(vertices, new Vector2(obj.X * 32, obj.Y * 32), new Vector2(32, 32), uv, Color.White);
                                }
                            }
                        }
                    }
                }
            }

            chunk.Update(System.Runtime.InteropServices.CollectionsMarshal.AsSpan(vertices));
        }

        private void AddQuad(List<Vertex> vertices, Vector2 pos, Vector2 size, Box2 uv, Color color)
        {
            vertices.Add(new Vertex(pos, new Vector2(uv.Left, uv.Top), color));
            vertices.Add(new Vertex(pos + new Vector2(size.X, 0), new Vector2(uv.Right, uv.Top), color));
            vertices.Add(new Vertex(pos + size, new Vector2(uv.Right, uv.Bottom), color));

            vertices.Add(new Vertex(pos + size, new Vector2(uv.Right, uv.Bottom), color));
            vertices.Add(new Vertex(pos + new Vector2(0, size.Y), new Vector2(uv.Left, uv.Bottom), color));
            vertices.Add(new Vertex(pos, new Vector2(uv.Left, uv.Top), color));
        }

        private float GetLayer(GameObject obj)
        {
            var layer = obj.GetVariable("layer");
            return layer.Type == DreamValueType.Float ? layer.AsFloat() : 2.0f;
        }

        private string? GetIcon(GameObject obj)
        {
            var icon = obj.GetVariable("Icon");
            return icon.Type == DreamValueType.String && icon.TryGetValue(out string? iconStr) ? iconStr : null;
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
