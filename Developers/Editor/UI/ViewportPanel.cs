using Shared;
using Silk.NET.OpenGL;
using ImGuiNET;
using System.Numerics;
using Robust.Shared.Maths;
using System.IO;

namespace Editor.UI
{
    public class ViewportPanel : IDisposable
    {
        private GL? _gl;
        private readonly ToolManager _toolManager;
        private readonly SelectionManager _selectionManager;
        private readonly EditorContext _editorContext;
        private readonly SpriteRenderer _spriteRenderer;
        private readonly TextureManager _textureManager;
        private readonly IGameApi _gameApi;
        private readonly IObjectTypeManager _objectTypeManager;
        private readonly IEditorSettingsManager _settingsManager;

        public ViewportPanel(ToolManager toolManager, SelectionManager selectionManager, EditorContext editorContext, IGameApi gameApi, SpriteRenderer spriteRenderer, TextureManager textureManager, IObjectTypeManager objectTypeManager, IEditorSettingsManager settingsManager)
        {
            _toolManager = toolManager;
            _selectionManager = selectionManager;
            _editorContext = editorContext;
            _spriteRenderer = spriteRenderer;
            _textureManager = textureManager;
            _gameApi = gameApi;
            _objectTypeManager = objectTypeManager;
            _settingsManager = settingsManager;
        }

        public void Initialize(GL gl)
        {
            _gl = gl;
        }

        public async void Draw(Scene scene)
        {
            // This is not ideal, but for now we'll reload the map if the scene's gamestate doesn't have it.
            if (scene.GameState.Map == null && File.Exists(scene.FilePath))
            {
                try
                {
                    var map = await _gameApi.Map.LoadMapAsync(scene.FilePath);
                    scene.GameState.Map = map;
                }
                catch (System.Exception e)
                {
                    System.Console.WriteLine($"[ERROR] Failed to load map: {e.Message}");
                }
            }

            ImGui.Begin("Viewport");

            if (ImGui.BeginDragDropTarget())
            {
                var payload = ImGui.AcceptDragDropPayload("OBJECT_TYPE_PAYLOAD");
                unsafe
                {
                    if (payload.NativePtr != null)
                    {
                        var objectTypeName = System.Text.Encoding.UTF8.GetString((byte*)payload.Data, payload.DataSize);
                        var objectType = _objectTypeManager.GetObjectType(objectTypeName);
                        if(objectType == null)
                            return;

                        var mousePos = ImGui.GetMousePos();
                        var windowPos = ImGui.GetWindowPos();
                        var localMousePos = new Vector2(mousePos.X - windowPos.X, mousePos.Y - windowPos.Y);
                        var worldMousePos = Camera.ScreenToWorld(localMousePos, Camera.GetProjectionMatrix(ImGui.GetWindowSize().X, ImGui.GetWindowSize().Y));
                        var tilePos = new Vector2i((int)(worldMousePos.X / EditorConstants.TileSize), (int)(worldMousePos.Y / EditorConstants.TileSize));
                        _gameApi.Objects.CreateObject(objectType.Id, tilePos.X, tilePos.Y, _editorContext.CurrentZLevel);
                    }
                }
                ImGui.EndDragDropTarget();
            }

            var windowSize = ImGui.GetWindowSize();
            var projectionMatrix = Camera.GetProjectionMatrix(windowSize.X, windowSize.Y);

            var currentMap = scene.GameState.Map;
            if (currentMap != null)
            {
                var settings = _settingsManager.Settings;
                if (settings.ShowGrid)
                {
                    DrawGrid(windowSize, projectionMatrix, settings);
                }

                _spriteRenderer.Begin(projectionMatrix);

                // Simple frustum culling
                var topLeftWorld = Camera.ScreenToWorld(Vector2.Zero, projectionMatrix);
                var bottomRightWorld = Camera.ScreenToWorld(windowSize, projectionMatrix);

                int minTileX = (int)Math.Floor(topLeftWorld.X / EditorConstants.TileSize);
                int maxTileX = (int)Math.Ceiling(bottomRightWorld.X / EditorConstants.TileSize);
                int minTileY = (int)Math.Floor(topLeftWorld.Y / EditorConstants.TileSize);
                int maxTileY = (int)Math.Ceiling(bottomRightWorld.Y / EditorConstants.TileSize);

                foreach (var (chunkCoords, chunk) in currentMap.GetChunks(_editorContext.CurrentZLevel))
                {
                    int chunkWorldX = chunkCoords.X * Chunk.ChunkSize;
                    int chunkWorldY = chunkCoords.Y * Chunk.ChunkSize;

                    if (chunkWorldX + Chunk.ChunkSize < minTileX || chunkWorldX > maxTileX ||
                        chunkWorldY + Chunk.ChunkSize < minTileY || chunkWorldY > maxTileY)
                        continue;

                    for (int y = 0; y < Chunk.ChunkSize; y++)
                    {
                        int worldY = chunkWorldY + y;
                        if (worldY < minTileY || worldY > maxTileY) continue;

                        for (int x = 0; x < Chunk.ChunkSize; x++)
                        {
                            int worldX = chunkWorldX + x;
                            if (worldX < minTileX || worldX > maxTileX) continue;

                            var turf = chunk.GetTurf(x, y);
                            if (turf != null)
                            {
                                foreach (var gameObject in turf.Contents)
                                {
                                    var spritePath = gameObject.GetVariable("SpritePath").ToString();
                                    if (!string.IsNullOrEmpty(spritePath))
                                    {
                                        uint textureId = _textureManager.GetTexture(spritePath);
                                        if (textureId != 0)
                                        {
                                            _spriteRenderer.Draw(textureId,
                                                new Vector2(worldX * EditorConstants.TileSize, worldY * EditorConstants.TileSize),
                                                new Vector2(EditorConstants.TileSize, EditorConstants.TileSize),
                                                Vector4.One, new Box2(0, 0, 1, 1));
                                        }
                                    }
                                }
                            }
                        }
                    }
                }

                _spriteRenderer.End();

                if (ImGui.IsWindowHovered())
                {
                    var mousePos = ImGui.GetMousePos();
                    var windowPos = ImGui.GetWindowPos();
                    var localMousePos = new Vector2(mousePos.X - windowPos.X, mousePos.Y - windowPos.Y);
                    var worldMousePos = Camera.ScreenToWorld(localMousePos, projectionMatrix);
                    var worldMousePosInt = new Vector2i((int)worldMousePos.X, (int)worldMousePos.Y);

                    // Panning
                    if (ImGui.IsMouseDragging(ImGuiMouseButton.Middle))
                    {
                        var delta = ImGui.GetIO().MouseDelta;
                        Camera.Position -= delta / Camera.Zoom;
                    }

                    // Zooming
                    float scrollDelta = ImGui.GetIO().MouseWheel;
                    if (scrollDelta != 0)
                    {
                        float zoomFactor = 1.1f;
                        if (scrollDelta < 0) Camera.Zoom /= zoomFactor;
                        else Camera.Zoom *= zoomFactor;
                        Camera.Zoom = Math.Clamp(Camera.Zoom, 0.1f, 10.0f);
                    }

                    _toolManager.OnMouseMove(_editorContext, scene.GameState, _selectionManager, worldMousePosInt);
                    if (ImGui.IsMouseClicked(ImGuiMouseButton.Left))
                    {
                        _toolManager.OnMouseDown(_editorContext, scene.GameState, _selectionManager, worldMousePosInt);
                    }
                    if (ImGui.IsMouseReleased(ImGuiMouseButton.Left))
                    {
                        _toolManager.OnMouseUp(_editorContext, scene.GameState, _selectionManager, worldMousePosInt);
                    }
                }

                _toolManager.Draw(_editorContext, scene.GameState, _selectionManager);
            }

            ImGui.End();
        }

        private void DrawGrid(Vector2 windowSize, Matrix4x4 projectionMatrix, EditorSettings settings)
        {
            var drawList = ImGui.GetWindowDrawList();
            var windowPos = ImGui.GetWindowPos();
            var gridSize = settings.GridSize;
            var color = ImGui.ColorConvertFloat4ToU32(settings.GridColor);

            // Calculate visible world bounds
            var topLeftWorld = Camera.ScreenToWorld(Vector2.Zero, projectionMatrix);
            var bottomRightWorld = Camera.ScreenToWorld(windowSize, projectionMatrix);

            int startX = ((int)topLeftWorld.X / gridSize) * gridSize;
            int startY = ((int)topLeftWorld.Y / gridSize) * gridSize;
            int endX = (int)bottomRightWorld.X;
            int endY = (int)bottomRightWorld.Y;

            for (int x = startX; x <= endX; x += gridSize)
            {
                var p1i = Camera.WorldToScreen(new Vector2d(x, topLeftWorld.Y), projectionMatrix);
                var p2i = Camera.WorldToScreen(new Vector2d(x, bottomRightWorld.Y), projectionMatrix);
                var p1 = new Vector2(p1i.X, p1i.Y);
                var p2 = new Vector2(p2i.X, p2i.Y);
                drawList.AddLine(windowPos + p1, windowPos + p2, color);
            }

            for (int y = startY; y <= endY; y += gridSize)
            {
                var p1i = Camera.WorldToScreen(new Vector2d(topLeftWorld.X, y), projectionMatrix);
                var p2i = Camera.WorldToScreen(new Vector2d(bottomRightWorld.X, y), projectionMatrix);
                var p1 = new Vector2(p1i.X, p1i.Y);
                var p2 = new Vector2(p2i.X, p2i.Y);
                drawList.AddLine(windowPos + p1, windowPos + p2, color);
            }
        }

        public void Dispose()
        {
            _spriteRenderer.Dispose();
            _textureManager.Dispose();
        }
    }
}
