using Shared.Enums;
using Shared.Models;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Silk.NET.OpenGL;
using Robust.Shared.Maths;
using System.Numerics;
using Shared;
using Core.Dmi;
using Client.Assets;
using Shared.Interfaces;

namespace Client.Graphics
{
    public class WorldRenderer : IDisposable
    {
        private readonly GL _gl;
        private readonly Shader _chunkShader;
        private readonly GraphicsResourceManager _resourceManager;
        private readonly InstancedSpriteRenderer _instancedRenderer;
        private readonly List<IGameObject> _renderObjectBuffer = new();

        private struct RenderItem
        {
            public Vector2 Position;
            public Vector2 Size;
            public Box2 Uv;
            public Color Color;
            public float Layer;
            public int ArrayLayer;
        }

        /// <summary>
        /// A reusable bucket for RenderItems to eliminate per-frame allocations.
        /// </summary>
        private class RenderBucket
        {
            private RenderItem[] _items = new RenderItem[64];
            private int _count = 0;

            public int Count => _count;
            public ReadOnlySpan<RenderItem> Items => _items.AsSpan(0, _count);

            public void Add(RenderItem item)
            {
                if (_count >= _items.Length)
                {
                    Array.Resize(ref _items, _items.Length * 2);
                }
                _items[_count++] = item;
            }

            public void Clear() => _count = 0;
        }

        // Layer buckets to avoid sorting on every frame
        private readonly RenderBucket[] _layerBuckets = Enumerable.Range(0, 32).Select(_ => new RenderBucket()).ToArray();

        // New: Support for grouped texture arrays
        private TextureArray? _mainTextureArray;
        private readonly Dictionary<uint, int> _textureToArrayLayer = new();

        private readonly Dictionary<Vector2i, RenderChunk> _chunks = new();
        private readonly HashSet<Vector2i> _visibleChunks = new();
        private readonly ConcurrentQueue<(RenderChunk Chunk, Vertex[] Vertices)> _pendingUploads = new();
        private readonly HashSet<Vector2i> _rebuildingChunks = new();

        public WorldRenderer(GL gl, GraphicsResourceManager resourceManager)
        {
            _gl = gl;
            _resourceManager = resourceManager;
            _instancedRenderer = new InstancedSpriteRenderer(_gl);

            string vert = @"#version 330 core
layout (location = 0) in vec2 aPos;
layout (location = 1) in vec2 aTexCoords;
layout (location = 2) in vec4 aColor;
out vec2 TexCoord;
out vec4 vColor;
uniform mat4 uProjection;
uniform mat4 uView;
void main() {
    TexCoord = aTexCoords;
    vColor = aColor;
    gl_Position = uProjection * uView * vec4(aPos, 0.0, 1.0);
}";
            string frag = @"#version 330 core
layout (location = 0) out vec4 gAlbedo;
in vec2 TexCoord;
in vec4 vColor;
uniform sampler2D uTexture;
void main() {
    vec4 texColor = texture(uTexture, TexCoord);
    if(texColor.a < 0.01) discard;
    gAlbedo = texColor * vColor;
}";
            _chunkShader = new Shader(_gl, vert, frag);

            // Initialize a large texture array for icons (e.g. 32x32, 2048 layers)
            _mainTextureArray = new TextureArray(_gl, 32, 32, 2048);
        }

        public void Render(GameState? previousState, GameState currentState, float alpha, Box2 cullRect, Matrix4x4 view, Matrix4x4 projection)
        {
            ProcessPendingUploads();
            UpdateVisibleChunks(cullRect);

            _chunkShader.Use();
            _chunkShader.SetUniform("uView", view);
            _chunkShader.SetUniform("uProjection", projection);

            foreach (var coords in _visibleChunks)
            {
                if (!_chunks.TryGetValue(coords, out var chunk))
                {
                    chunk = new RenderChunk(_gl, coords);
                    _chunks[coords] = chunk;
                }

                if (chunk.IsDirty && !_rebuildingChunks.Contains(coords))
                {
                    _rebuildingChunks.Add(coords);
                    var stateClone = currentState;
                    Task.Run(() => RebuildChunkTask(chunk, stateClone));
                }

                chunk.Draw();
            }

            RenderDynamicObjects(previousState, currentState, alpha, cullRect, view, projection);
        }

        /// <summary>
        /// Optimized rendering of dynamic objects using Hybrid Spatial-SoA approach and layer bucketing.
        /// Uses SpatialGrid for fast culling and Archetype SoA for zero-allocation data access.
        /// </summary>
        /// <param name="previousState">The state from the previous tick.</param>
        /// <param name="currentState">The current simulation state.</param>
        /// <param name="alpha">Interpolation factor between frames.</param>
        /// <param name="cullRect">The visible world bounds in tile coordinates.</param>
        /// <param name="view">The view matrix.</param>
        /// <param name="projection">The projection matrix.</param>
        private void RenderDynamicObjects(GameState? previousState, GameState currentState, float alpha, Box2 cullRect, Matrix4x4 view, Matrix4x4 projection)
        {
            _instancedRenderer.Begin();

            ClearBuckets();
            PopulateBuckets(currentState, cullRect);
            DrawBuckets();

            _instancedRenderer.Flush(view, projection);
        }

        private void ClearBuckets()
        {
            for (int i = 0; i < _layerBuckets.Length; i++) _layerBuckets[i].Clear();
        }

        private void PopulateBuckets(GameState currentState, Box2 cullRect)
        {
            _renderObjectBuffer.Clear();
            currentState.SpatialGrid.QueryBox(new Box3l((long)cullRect.Left, (long)cullRect.Top, -100, (long)cullRect.Right, (long)cullRect.Bottom, 100), obj => _renderObjectBuffer.Add(obj));

            foreach (var obj in _renderObjectBuffer)
            {
                if (obj.Archetype is not Archetype arch) continue;
                int idx = obj.ArchetypeIndex;

                double layer = arch.GetLayer(idx);
                // Skip layers handled by RenderChunks (e.g. static turf layer 2.0)
                if (Math.Abs(layer - 2.0f) < 0.001f) continue;

                // Basic culling (already mostly done by SpatialGrid, but adding safety margin)
                if (obj.X < cullRect.Left - 1 || obj.X > cullRect.Right + 1 ||
                    obj.Y < cullRect.Top - 1 || obj.Y > cullRect.Bottom + 1)
                {
                    continue;
                }

                string icon = arch.GetIcon(idx);
                if (string.IsNullOrEmpty(icon)) continue;

                var (dmiPath, stateName) = _resourceManager.IconCache.ParseIconString(icon);
                var texture = _resourceManager.TextureCache.GetTexture(dmiPath.Replace(".dmi", ".png"));
                var dmi = _resourceManager.DmiCache.GetDmi(dmiPath, texture);

                if (dmi != null && _mainTextureArray != null)
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

                        Vector2 pos = new Vector2(arch.GetX(idx) * 32 + (float)arch.GetPixelX(idx), arch.GetY(idx) * 32 + (float)arch.GetPixelY(idx));
                        Color color = Color.FromHex(arch.GetColor(idx)).WithAlpha((float)arch.GetAlpha(idx) / 255.0f);

                        int bucketIdx = Math.Clamp((int)layer, 0, _layerBuckets.Length - 1);
                        _layerBuckets[bucketIdx].Add(new RenderItem
                        {
                            Position = pos,
                            Size = new Vector2(32, 32),
                            Uv = uv,
                            Color = color,
                            Layer = (float)layer,
                            ArrayLayer = 0 // Fixed for demo
                        });
                    }
                }
            }
        }

        private void DrawBuckets()
        {
            for (int i = 0; i < _layerBuckets.Length; i++)
            {
                var bucket = _layerBuckets[i];
                var items = bucket.Items;
                for (int j = 0; j < items.Length; j++)
                {
                    ref readonly var item = ref items[j];
                    _instancedRenderer.Draw(_mainTextureArray!.Id, item.ArrayLayer, item.Position, item.Size, item.Uv, item.Color);
                }
            }
        }

        private void ProcessPendingUploads()
        {
            while (_pendingUploads.TryDequeue(out var upload))
            {
                upload.Chunk.Update(upload.Vertices);
                _rebuildingChunks.Remove(upload.Chunk.Coords);
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

        /// <summary>
        /// Rebuilds static chunk geometry using Hybrid Spatial-SoA approach.
        /// Runs on a background thread.
        /// </summary>
        private void RebuildChunkTask(RenderChunk chunk, GameState state)
        {
            var vertices = new List<Vertex>();

            long startX = (long)chunk.Coords.X * RenderChunk.ChunkSize;
            long startY = (long)chunk.Coords.Y * RenderChunk.ChunkSize;
            long endX = startX + RenderChunk.ChunkSize;
            long endY = startY + RenderChunk.ChunkSize;

            var objects = new List<IGameObject>();
            state.SpatialGrid.QueryBox(new Box3l(startX, startY, -100, endX - 1, endY - 1, 100), obj => objects.Add(obj));

            foreach (var obj in objects)
            {
                if (obj.Archetype is not Archetype arch) continue;
                int idx = obj.ArchetypeIndex;

                double layer = arch.GetLayer(idx);
                // Turfs/Static objects are usually on layer 2.0
                if (Math.Abs(layer - 2.0f) > 0.001f) continue;

                string icon = arch.GetIcon(idx);
                if (string.IsNullOrEmpty(icon)) continue;

                var (dmiPath, stateName) = _resourceManager.IconCache.ParseIconString(icon);
                var texture = _resourceManager.TextureCache.GetTexture(dmiPath.Replace(".dmi", ".png"));
                var dmi = _resourceManager.DmiCache.GetDmi(dmiPath, texture);
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

                        AddQuad(vertices, new Vector2(arch.GetX(idx) * 32, arch.GetY(idx) * 32), new Vector2(32, 32), uv, Color.White);
                    }
                }
            }

            _pendingUploads.Enqueue((chunk, vertices.ToArray()));
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
            _chunkShader.Dispose();
            _instancedRenderer.Dispose();
            _mainTextureArray?.Dispose();
            foreach (var chunk in _chunks.Values)
            {
                chunk.Dispose();
            }
            _chunks.Clear();
        }

        public Dictionary<string, object> GetDiagnosticInfo()
        {
            int totalItems = 0;
            for (int i = 0; i < _layerBuckets.Length; i++) totalItems += _layerBuckets[i].Count;

            return new Dictionary<string, object>
            {
                ["ActiveChunks"] = _chunks.Count,
                ["VisibleChunks"] = _visibleChunks.Count,
                ["RebuildingChunks"] = _rebuildingChunks.Count,
                ["PendingUploads"] = _pendingUploads.Count,
                ["DynamicObjectsRendered"] = totalItems,
                ["TextureArrayDepth"] = _mainTextureArray?.Depth ?? 0
            };
        }
    }
}
