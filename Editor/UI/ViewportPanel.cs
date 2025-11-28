using Core;
using Silk.NET.OpenGL;
using ImGuiNET;
using Silk.NET.Maths;
using System.Numerics;
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
        private readonly Camera _camera;
        private readonly MapLoader _mapLoader;

        private GameState? _currentGameState;
        private string _currentFile = "";

        public ViewportPanel(GL gl, ToolManager toolManager, SelectionManager selectionManager, EditorContext editorContext, MapLoader mapLoader)
        {
            _gl = gl;
            _toolManager = toolManager;
            _selectionManager = selectionManager;
            _editorContext = editorContext;
            _spriteRenderer = new SpriteRenderer(_gl);
            _textureManager = new TextureManager(_gl);
            _camera = new Camera();
            _mapLoader = mapLoader;
        }

        public void Draw(string filePath)
        {
            if (_currentFile != filePath)
            {
                var map = _mapLoader.LoadMap(filePath);
                _currentGameState = new GameState { Map = map };
                _currentFile = filePath;
            }

            ImGui.Begin("Viewport");

            var windowSize = ImGui.GetWindowSize();
            _camera.Update(windowSize.X, windowSize.Y);

            if (_currentGameState?.Map is Map currentMap)
            {
                for (int y = 0; y < currentMap.Height; y++)
                {
                    for (int x = 0; x < currentMap.Width; x++)
                    {
                        var turf = currentMap.GetTurf(x, y, _editorContext.CurrentZLevel);
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
                                        _spriteRenderer.Draw(textureId, new Vector2D<int>(x * EditorConstants.TileSize, y * EditorConstants.TileSize), new Vector2D<int>(EditorConstants.TileSize, EditorConstants.TileSize), 0.0f, _camera.GetProjectionMatrix());
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

                    _toolManager.OnMouseMove(_editorContext, _currentGameState, _selectionManager, localMousePos);
                    if (ImGui.IsMouseClicked(ImGuiMouseButton.Left))
                    {
                        _toolManager.OnMouseDown(_editorContext, _currentGameState, _selectionManager, localMousePos);
                    }
                    if (ImGui.IsMouseReleased(ImGuiMouseButton.Left))
                    {
                        _toolManager.OnMouseUp(_editorContext, _currentGameState, _selectionManager, localMousePos);
                    }
                }

                _toolManager.Draw(_editorContext, _currentGameState, _selectionManager);
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
