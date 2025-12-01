using Core;
using Silk.NET.OpenGL;
using ImGuiNET;
using System.Numerics;
using Robust.Shared.Maths;
using System.IO;

namespace Editor.UI
{
    public class ViewportPanel : IDisposable
    {
        private readonly GL _gl;
        private readonly ToolManager _toolManager;
        private readonly SelectionManager _selectionManager;
        private readonly EditorContext _editorContext;
        private readonly SpriteRenderer _spriteRenderer;
        private readonly TextureManager _textureManager;
        private readonly GameApi _gameApi;

        private string _currentFile = "";

        public ViewportPanel(GL gl, ToolManager toolManager, SelectionManager selectionManager, EditorContext editorContext, GameApi gameApi)
        {
            _gl = gl;
            _toolManager = toolManager;
            _selectionManager = selectionManager;
            _editorContext = editorContext;
            _spriteRenderer = new SpriteRenderer(_gl);
            _textureManager = new TextureManager(_gl);
            _gameApi = gameApi;
        }

        public async void Draw(string filePath)
        {
            if (_currentFile != filePath)
            {
                await _gameApi.LoadMapAsync(filePath);
                _currentFile = filePath;
            }

            ImGui.Begin("Viewport");

            var windowSize = ImGui.GetWindowSize();
            var projectionMatrix = Camera.GetProjectionMatrix(windowSize.X, windowSize.Y);

            var currentMap = _gameApi.GetMap();
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


                    _toolManager.OnMouseMove(_editorContext, _gameApi.GetState(), _selectionManager, worldMousePosInt);
                    if (ImGui.IsMouseClicked(ImGuiMouseButton.Left))
                    {
                        _toolManager.OnMouseDown(_editorContext, _gameApi.GetState(), _selectionManager, worldMousePosInt);
                    }
                    if (ImGui.IsMouseReleased(ImGuiMouseButton.Left))
                    {
                        _toolManager.OnMouseUp(_editorContext, _gameApi.GetState(), _selectionManager, worldMousePosInt);
                    }
                }

                _toolManager.Draw(_editorContext, _gameApi.GetState(), _selectionManager);
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
