using Shared.Enums;
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
        private readonly TextureCache _textureCache;
        private readonly DmiCache _dmiCache;
        private readonly IconCache _iconCache;
        private readonly InstancedSpriteRenderer _instancedRenderer;
        private readonly List<IGameObject> _renderObjectBuffer = new();

        // New: Support for grouped texture arrays
        private TextureArray? _mainTextureArray;
        private readonly Dictionary<uint, int> _textureToArrayLayer = new();

        private readonly Dictionary<Vector2i, RenderChunk> _chunks = new();
        private readonly HashSet<Vector2i> _visibleChunks = new();
        private readonly ConcurrentQueue<(RenderChunk Chunk, Vertex[] Vertices)> _pendingUploads = new();
        private readonly HashSet<Vector2i> _rebuildingChunks = new();

        public WorldRenderer(GL gl, TextureCache textureCache, DmiCache dmiCache, IconCache iconCache)
        {
            _gl = gl;
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

            _textureCache = textureCache;
            _dmiCache = dmiCache;
            _iconCache = iconCache;

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

        private void RenderDynamicObjects(GameState? previousState, GameState currentState, float alpha, Box2 cullRect, Matrix4x4 view, Matrix4x4 projection)
        {
            _instancedRenderer.Begin();
            _renderObjectBuffer.Clear();

            currentState.SpatialGrid.QueryBox(new Box3l((long)cullRect.Left, (long)cullRect.Top, -100, (long)cullRect.Right, (long)cullRect.Bottom, 100), obj => _renderObjectBuffer.Add(obj));

            _renderObjectBuffer.Sort((a, b) => GetLayer(a).CompareTo(GetLayer(b)));

            foreach (var obj in _renderObjectBuffer)
            {
                var layer = GetLayer(obj);
                if (layer != 2.0f)
                {
                    if (obj.X < cullRect.Left - 1 || obj.X > cullRect.Right + 1 ||
                        obj.Y < cullRect.Top - 1 || obj.Y > cullRect.Bottom + 1)
                    {
                        continue;
                    }

                    var icon = GetIcon(obj);
                    if (!string.IsNullOrEmpty(icon))
                    {
                        var (dmiPath, stateName) = _iconCache.ParseIconString(icon);
                        var texture = _textureCache.GetTexture(dmiPath.Replace(".dmi", ".png"));
                        var dmi = _dmiCache.GetDmi(dmiPath, texture);

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

                                Vector2 pos = new Vector2(obj.X * 32 + (float)obj.PixelX, obj.Y * 32 + (float)obj.PixelY);
                                Color color = Color.FromHex(obj.Color).WithAlpha((float)obj.Alpha / 255.0f);

                                // Simplified layer mapping for demo
                                int arrayLayer = 0;
                                _instancedRenderer.Draw(_mainTextureArray.Id, arrayLayer, pos, new Vector2(32, 32), uv, color);
                            }
                        }
                    }
                }
            }

            _instancedRenderer.Flush(view, projection);
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

        private void RebuildChunkTask(RenderChunk chunk, GameState state)
        {
            var vertices = new List<Vertex>();
            var objects = new List<IGameObject>();

            long startX = (long)chunk.Coords.X * RenderChunk.ChunkSize;
            long startY = (long)chunk.Coords.Y * RenderChunk.ChunkSize;
            long endX = startX + RenderChunk.ChunkSize;
            long endY = startY + RenderChunk.ChunkSize;

            state.SpatialGrid.QueryBox(new Box3l(startX, startY, -100, endX - 1, endY - 1, 100), obj => objects.Add(obj));

            foreach (var obj in objects)
            {
                var layer = GetLayer(obj);
                if (layer == 2.0f)
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

        private float GetLayer(IGameObject obj)
        {
            var layer = obj.GetVariable("layer");
            return layer.Type == DreamValueType.Float ? layer.AsFloat() : 2.0f;
        }

        private string? GetIcon(IGameObject obj)
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
            _chunkShader.Dispose();
            _instancedRenderer.Dispose();
            _mainTextureArray?.Dispose();
            foreach (var chunk in _chunks.Values)
            {
                chunk.Dispose();
            }
            _chunks.Clear();
        }
    }
}
