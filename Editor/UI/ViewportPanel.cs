
using ImGuiNET;
using Core;
using Silk.NET.Maths;
using System.Numerics;

namespace Editor.UI
{
    public class ViewportPanel
    {
        private readonly GameState _gameState;
        private readonly SpriteRenderer _spriteRenderer;
        private readonly TextureManager _textureManager;
        private readonly ToolManager _toolManager;
        private readonly Editor _editor;
        private readonly GameApi _gameApi;
        private readonly SelectionManager _selectionManager;

        public ViewportPanel(GameState gameState, SpriteRenderer spriteRenderer, TextureManager textureManager, ToolManager toolManager, Editor editor, GameApi gameApi, SelectionManager selectionManager)
        {
            _gameState = gameState;
            _spriteRenderer = spriteRenderer;
            _textureManager = textureManager;
            _toolManager = toolManager;
            _editor = editor;
            _gameApi = gameApi;
            _selectionManager = selectionManager;
        }

        public void Draw()
        {
            ImGui.Begin("Viewport");
            HandleMouseInput();
            DrawMapContent();
            ImGui.End();
        }

        private void DrawMapContent()
        {
            var windowSize = ImGui.GetWindowSize();
            var projection = Matrix4X4.CreateOrthographicOffCenter(0.0f, windowSize.X, windowSize.Y, 0.0f, -1.0f, 1.0f);

            if (_gameState.Map != null && _spriteRenderer != null && _textureManager != null && _editor.CurrentZLevel < _gameState.Map.Depth)
            {
                for (int y = 0; y < _gameState.Map.Height; y++)
                {
                    for (int x = 0; x < _gameState.Map.Width; x++)
                    {
                        var turf = _gameState.Map.GetTurf(x, y, _editor.CurrentZLevel);
                        if (turf == null) continue;
                        foreach (var gameObject in turf.Contents)
                        {
                            var spritePath = gameObject.GetProperty<string>("SpritePath");
                            if (string.IsNullOrEmpty(spritePath)) continue;
                            uint textureId = _textureManager.GetTexture(spritePath);
                            if (textureId != 0)
                            {
                                _spriteRenderer.Draw(textureId, new Vector2D<int>(x * Constants.TileSize, y * Constants.TileSize), new Vector2D<int>(Constants.TileSize, Constants.TileSize), 0.0f, projection);
                            }
                        }
                    }
                }
            }
            _toolManager.Draw(_editor, _gameApi, _gameState, _selectionManager);
        }

        private void HandleMouseInput()
        {
            if (!ImGui.IsWindowHovered(ImGuiHoveredFlags.None)) return;

            var mousePos = ImGui.GetMousePos();
            var windowPos = ImGui.GetWindowPos();
            var localMousePos = new Vector2D<int>((int)(mousePos.X - windowPos.X), (int)(mousePos.Y - windowPos.Y));

            _toolManager.OnMouseMove(_editor, _gameApi, _gameState, _selectionManager, localMousePos);
            if (ImGui.IsMouseClicked(ImGuiMouseButton.Left))
            {
                _toolManager.OnMouseDown(_editor, _gameApi, _gameState, _selectionManager, localMousePos);
            }
            if (ImGui.IsMouseReleased(ImGuiMouseButton.Left))
            {
                _toolManager.OnMouseUp(_editor, _gameApi, _gameState, _selectionManager, localMousePos);
            }
        }
    }
}
