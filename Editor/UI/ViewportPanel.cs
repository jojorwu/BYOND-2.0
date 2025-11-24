using Core;
using Silk.NET.OpenGL;
using ImGuiNET;
using Silk.NET.Maths;
using System.Numerics;

namespace Editor.UI
{
    public class ViewportPanel : IDisposable
    {
        private readonly GL _gl;
        private readonly GameApi _gameApi;
        private readonly ToolManager _toolManager;
        private readonly SelectionManager _selectionManager;
        private readonly EditorContext _editorContext;
        private readonly SpriteRenderer _spriteRenderer;
        private readonly TextureManager _textureManager;
        private readonly Camera _camera;

        public ViewportPanel(GL gl, GameApi gameApi, ToolManager toolManager, SelectionManager selectionManager, EditorContext editorContext)
        {
            _gl = gl;
            _gameApi = gameApi;
            _toolManager = toolManager;
            _selectionManager = selectionManager;
            _editorContext = editorContext;
            _spriteRenderer = new SpriteRenderer(_gl);
            _textureManager = new TextureManager(_gl);
            _camera = new Camera();
        }

        public void Draw()
        {
            ImGui.Begin("Viewport");

            var windowSize = ImGui.GetWindowSize();
            _camera.Update(windowSize.X, windowSize.Y);

            if (_gameApi.GetMap() is Map map)
            {
                for (int y = 0; y < map.Height; y++)
                {
                    for (int x = 0; x < map.Width; x++)
                    {
                        var turf = map.GetTurf(x, y, _editorContext.CurrentZLevel);
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
                                        _spriteRenderer.Draw(textureId, new Vector2D<int>(x * Constants.TileSize, y * Constants.TileSize), new Vector2D<int>(Constants.TileSize, Constants.TileSize), 0.0f, _camera.GetProjectionMatrix());
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
                var localMousePos = new Vector2D<int>((int)(mousePos.X - windowPos.X), (int)(mousePos.Y - windowPos.Y));

                _toolManager.OnMouseMove(_editorContext, _gameApi.GetState(), _selectionManager, localMousePos);
                if (ImGui.IsMouseClicked(ImGuiMouseButton.Left))
                {
                    _toolManager.OnMouseDown(_editorContext, _gameApi.GetState(), _selectionManager, localMousePos);
                }
                if (ImGui.IsMouseReleased(ImGuiMouseButton.Left))
                {
                    _toolManager.OnMouseUp(_editorContext, _gameApi.GetState(), _selectionManager, localMousePos);
                }
            }

            _toolManager.Draw(_editorContext, _gameApi.GetState(), _selectionManager);

            ImGui.End();
        }

        public void Dispose()
        {
            _spriteRenderer.Dispose();
            _textureManager.Dispose();
        }
    }
}
