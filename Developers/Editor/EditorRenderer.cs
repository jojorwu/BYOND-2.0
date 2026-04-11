using Silk.NET.OpenGL;
using System.Numerics;
using Shared;
using Shared.Attributes;
using Shared.Services;
using Shared.Interfaces;
using Client.Graphics;
using Robust.Shared.Maths;

namespace Editor;

/// <summary>
/// Foundation for Editor-specific world rendering, grids, and gizmos.
/// </summary>
[EngineService]
public class EditorRenderer : EngineService, IDisposable
{
    private GL? _gl;
    private readonly IGameState _gameState;
    private readonly IArchetypeManager _archetypeManager;
    private WorldRenderer? _worldRenderer;
    private Client.Graphics.Framebuffer? _viewportBuffer;
    private Camera _camera = new(Vector2.Zero, 1.0f);

    public uint ViewportTexture => _viewportBuffer?.Texture ?? 0;
    public Camera Camera => _camera;

    public EditorRenderer(IGameState gameState, IArchetypeManager archetypeManager)
    {
        _gameState = gameState;
        _archetypeManager = archetypeManager;
    }

    public void Initialize(GL gl, GraphicsResourceManager resourceManager)
    {
        _gl = gl;
        _worldRenderer = new WorldRenderer(_gl, resourceManager);
        _viewportBuffer = new Client.Graphics.Framebuffer(_gl, 1280, 720);
    }

    public void Resize(int width, int height)
    {
        if (width <= 0 || height <= 0) return;
        _viewportBuffer?.Dispose();
        _viewportBuffer = new Client.Graphics.Framebuffer(_gl!, width, height);
    }

    public void Render(float dt)
    {
        if (_gl == null || _worldRenderer == null || _viewportBuffer == null) return;

        _viewportBuffer.Bind();
        _gl.ClearColor(0.15f, 0.15f, 0.15f, 1.0f);
        _gl.Clear(ClearBufferMask.ColorBufferBit | ClearBufferMask.DepthBufferBit);

        var view = _camera.GetViewMatrix();
        var projection = _camera.GetProjectionMatrix((float)_viewportBuffer.Width, (float)_viewportBuffer.Height);

        // Simple cull rect for editor
        var cullRect = new Box2(-1000, -1000, 1000, 1000);

        _worldRenderer.Render(dt, null, (GameState)_gameState, 1.0f, cullRect, view, projection);

        RenderGrid(32, 0.2f);
        RenderGizmos();

        _viewportBuffer.Unbind();
    }

    public void RenderGrid(int size, float opacity)
    {
        // TODO: Implement grid rendering
    }

    public void RenderGizmos()
    {
        // TODO: Implement gizmo rendering
    }

    public void HighlightSelection(long entityId)
    {
        // TODO: Implement selection highlighting
    }

    public void Dispose()
    {
        _worldRenderer?.Dispose();
        _viewportBuffer?.Dispose();
    }
}
