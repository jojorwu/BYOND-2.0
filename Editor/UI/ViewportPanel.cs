
using ImGuiNET;
using Core;
using Silk.NET.Maths;
using System.Numerics;
using System.Collections.Generic;
using System.Linq;

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
        private readonly EditorContext _editorContext;
        private readonly Camera _camera;

        public ViewportPanel(GameState gameState, SpriteRenderer spriteRenderer, TextureManager textureManager, ToolManager toolManager, Editor editor, GameApi gameApi, SelectionManager selectionManager, EditorContext editorContext, Camera camera)
        {
            _editorContext = editorContext;
            _gameState = gameState;
            _spriteRenderer = spriteRenderer;
            _textureManager = textureManager;
            _toolManager = toolManager;
            _editor = editor;
            _gameApi = gameApi;
            _selectionManager = selectionManager;
            _camera = camera;
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
            var projection = Matrix4x4.CreateOrthographicOffCenter(0.0f, windowSize.X, windowSize.Y, 0.0f, -1.0f, 1.0f);
            var view = _camera.GetViewMatrix();
            var viewProjection = view * projection;

            var sprites = new List<SpriteInstance>();

            if (_gameState.Map != null && _spriteRenderer != null && _textureManager != null && _editor.CurrentZLevel < _gameState.Map.Depth)
            {
                var viewBounds = GetViewBounds(windowSize);
                for (int y = viewBounds.minY; y < viewBounds.maxY; y++)
                {
                    for (int x = viewBounds.minX; x < viewBounds.maxX; x++)
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
                                var model = Matrix4x4.Identity;
                                model *= Matrix4x4.CreateTranslation(x * Constants.TileSize, y * Constants.TileSize, 0.0f);
                                model *= Matrix4x4.CreateScale(Constants.TileSize, Constants.TileSize, 1.0f);
                                sprites.Add(new SpriteInstance { Model = model, TextureId = textureId });
                            }
                        }
                    }
                }
            }

            sprites.Sort((a, b) => a.TextureId.CompareTo(b.TextureId));

            if (_spriteRenderer != null)
            {
                _spriteRenderer.Draw(sprites, viewProjection);
            }

            DrawHoverHighlight(viewProjection);

            _toolManager.Draw(_editor, _editorContext, _gameApi, _gameState, _selectionManager);
        }

        private void DrawHoverHighlight(Matrix4x4 viewProjection)
        {
            if (!ImGui.IsWindowHovered(ImGuiHoveredFlags.None)) return;

            var mousePos = ImGui.GetMousePos();
            var windowPos = ImGui.GetWindowPos();
            var localMousePos = new Vector2(mousePos.X - windowPos.X, mousePos.Y - windowPos.Y);

            var inverseVp = new Matrix4x4();
            Matrix4x4.Invert(viewProjection, out inverseVp);
            var worldPos = Vector2.Transform(localMousePos, inverseVp);

            var tileX = (int)MathF.Floor(worldPos.X / Constants.TileSize);
            var tileY = (int)MathF.Floor(worldPos.Y / Constants.TileSize);

            var drawList = ImGui.GetWindowDrawList();
            var min = Vector2.Transform(new Vector2(tileX * Constants.TileSize, tileY * Constants.TileSize), viewProjection);
            var max = Vector2.Transform(new Vector2((tileX + 1) * Constants.TileSize, (tileY + 1) * Constants.TileSize), viewProjection);

            drawList.AddRect(windowPos + min, windowPos + max, 0x80FFFFFF, 0, ImDrawFlags.None, 1.0f);
        }

        private (int minX, int minY, int maxX, int maxY) GetViewBounds(Vector2 windowSize)
        {
            if (_gameState.Map == null) return (0, 0, 0, 0);

            var inverseView = new Matrix4x4();
            Matrix4x4.Invert(_camera.GetViewMatrix(), out inverseView);

            var topLeft = Vector2.Transform(Vector2.Zero, inverseView) / Constants.TileSize;
            var topRight = Vector2.Transform(new Vector2(windowSize.X, 0), inverseView) / Constants.TileSize;
            var bottomLeft = Vector2.Transform(new Vector2(0, windowSize.Y), inverseView) / Constants.TileSize;
            var bottomRight = Vector2.Transform(windowSize, inverseView) / Constants.TileSize;

            var minX = (int)MathF.Max(0, MathF.Min(topLeft.X, MathF.Min(topRight.X, MathF.Min(bottomLeft.X, bottomRight.X))));
            var minY = (int)MathF.Max(0, MathF.Min(topLeft.Y, MathF.Min(topRight.Y, MathF.Min(bottomLeft.Y, bottomRight.Y))));
            var maxX = (int)MathF.Min(_gameState.Map.Width, MathF.Max(topLeft.X, MathF.Max(topRight.X, MathF.Max(bottomLeft.X, bottomRight.X))) + 1);
            var maxY = (int)MathF.Min(_gameState.Map.Height, MathF.Max(topLeft.Y, MathF.Max(topRight.Y, MathF.Max(bottomLeft.Y, bottomRight.Y))) + 1);

            return (minX, minY, maxX, maxY);
        }

        private void HandleMouseInput()
        {
            if (!ImGui.IsWindowHovered(ImGuiHoveredFlags.None)) return;

            if (ImGui.IsMouseDown(ImGuiMouseButton.Middle))
            {
                _camera.Move(-ImGui.GetIO().MouseDelta);
            }

            var mousePos = ImGui.GetMousePos();
            var windowPos = ImGui.GetWindowPos();
            var localMousePos = new Vector2D<int>((int)(mousePos.X - windowPos.X), (int)(mousePos.Y - windowPos.Y));

            var inverseView = new Matrix4x4();
            Matrix4x4.Invert(_camera.GetViewMatrix(), out inverseView);
            var worldMousePos = Vector2.Transform(new Vector2(localMousePos.X, localMousePos.Y), inverseView);
            var worldMousePosVec2D = new Vector2D<int>((int)worldMousePos.X, (int)worldMousePos.Y);

            _toolManager.OnMouseMove(_editor, _editorContext, _gameApi, _gameState, _selectionManager, worldMousePosVec2D);
            if (ImGui.IsMouseClicked(ImGuiMouseButton.Left))
            {
                _toolManager.OnMouseDown(_editor, _editorContext, _gameApi, _gameState, _selectionManager, worldMousePosVec2D);
            }
            if (ImGui.IsMouseReleased(ImGuiMouseButton.Left))
            {
                _toolManager.OnMouseUp(_editor, _editorContext, _gameApi, _gameState, _selectionManager, worldMousePosVec2D);
            }
        }
    }
}
