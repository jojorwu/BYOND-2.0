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

        public ViewportPanel(ToolManager toolManager, SelectionManager selectionManager, EditorContext editorContext, IGameApi gameApi, SpriteRenderer spriteRenderer, TextureManager textureManager, IObjectTypeManager objectTypeManager)
        {
            _toolManager = toolManager;
            _selectionManager = selectionManager;
            _editorContext = editorContext;
            _spriteRenderer = spriteRenderer;
            _textureManager = textureManager;
            _gameApi = gameApi;
            _objectTypeManager = objectTypeManager;
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
                        if (objectType != null)
                        {
                            var mousePos = ImGui.GetMousePos();
                            var windowPos = ImGui.GetWindowPos();
                            var localMousePos = new Vector2(mousePos.X - windowPos.X, mousePos.Y - windowPos.Y);
                            var worldMousePos = Camera.ScreenToWorld(localMousePos, Camera.GetProjectionMatrix(ImGui.GetWindowSize().X, ImGui.GetWindowSize().Y));
                            var tilePos = new Vector2i((int)(worldMousePos.X / EditorConstants.TileSize), (int)(worldMousePos.Y / EditorConstants.TileSize));
                            _gameApi.Objects.CreateObject(objectType.Id, tilePos.X, tilePos.Y, _editorContext.CurrentZLevel);
                        }
                    }
                }
                ImGui.EndDragDropTarget();
            }

            var windowSize = ImGui.GetWindowSize();
            var projectionMatrix = Camera.GetProjectionMatrix(windowSize.X, windowSize.Y);

            var currentMap = scene.GameState.Map;
            if (currentMap != null)
            {
                foreach (var (chunkCoords, chunk) in currentMap.GetChunks(_editorContext.CurrentZLevel))
                {
                    for (int y = 0; y < Chunk.ChunkSize; y++)
                    {
                        for (int x = 0; x < Chunk.ChunkSize; x++)
                        {
                            var turf = chunk.GetTurf(x, y);
                            if (turf != null)
                            {
                                foreach (var gameObject in turf.Contents)
                                {
                                    var spritePath = gameObject.GetProperty<string>("SpritePath");
                                    if (!string.IsNullOrEmpty(spritePath))
                                    {
                                        uint textureId = _textureManager.GetTexture(spritePath);
                                        if (textureId != 0)
                                        {
                                            var worldX = chunkCoords.X * Chunk.ChunkSize + x;
                                            var worldY = chunkCoords.Y * Chunk.ChunkSize + y;
                                            _spriteRenderer.Draw(textureId, new Vector2i(worldX * EditorConstants.TileSize, worldY * EditorConstants.TileSize), new Vector2i(EditorConstants.TileSize, EditorConstants.TileSize), 0.0f, projectionMatrix);
                                        }
                                    }
                                }
                            }
                        }
                    }
                }

                if (ImGui.IsWindowHovered())
                {
                    var mousePos = ImGui.GetMousePos();
                    var windowPos = ImGui.GetWindowPos();
                    var localMousePos = new Vector2(mousePos.X - windowPos.X, mousePos.Y - windowPos.Y);
                    var worldMousePos = Camera.ScreenToWorld(localMousePos, projectionMatrix);
                    var worldMousePosInt = new Vector2i((int)worldMousePos.X, (int)worldMousePos.Y);


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

        public void Dispose()
        {
            _spriteRenderer.Dispose();
            _textureManager.Dispose();
        }
    }
}
