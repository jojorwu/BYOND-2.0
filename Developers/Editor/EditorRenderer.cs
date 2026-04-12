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
public class EditorRenderer : EngineService
{
    private GL? _gl;
    private readonly IGameState _gameState;
    private readonly EditorState _state;
    private readonly IArchetypeManager _archetypeManager;
    private WorldRenderer? _worldRenderer;
    private Client.Graphics.Framebuffer? _viewportBuffer;
    private Camera _camera = new(Vector2.Zero, 1.0f);
    private Client.Graphics.Shader? _gridShader;
    private uint _gridVao;
    private uint _gridVbo;

    public uint ViewportTexture => _viewportBuffer?.Texture ?? 0;
    public Camera Camera => _camera;

    public EditorRenderer(IGameState gameState, EditorState state, IArchetypeManager archetypeManager)
    {
        _gameState = gameState;
        _state = state;
        _archetypeManager = archetypeManager;
    }

    public void Initialize(GL gl, GraphicsResourceManager resourceManager)
    {
        _gl = gl;
        _worldRenderer = new WorldRenderer(_gl, resourceManager);
        _viewportBuffer = new Client.Graphics.Framebuffer(_gl, 1280, 720);
        InitializeGrid();
    }

    private void InitializeGrid()
    {
        if (_gl == null) return;

        string vertexSource = @"
            #version 330 core
            layout (location = 0) in vec3 aPos;
            uniform mat4 uView;
            uniform mat4 uProjection;
            void main() {
                gl_Position = uProjection * uView * vec4(aPos, 1.0);
            }";

        string fragmentSource = @"
            #version 330 core
            out vec4 FragColor;
            uniform vec4 uColor;
            void main() {
                FragColor = uColor;
            }";

        _gridShader = new Client.Graphics.Shader(_gl, vertexSource, fragmentSource);

        _gridVao = _gl.GenVertexArray();
        _gridVbo = _gl.GenBuffer();
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

        RenderGrid(_state.GridSize, 0.2f);
        RenderGizmos();

        _viewportBuffer.Unbind();
    }

    public void RenderGrid(int gridSize, float opacity)
    {
        if (_gl == null || _gridShader == null || _viewportBuffer == null) return;

        _gridShader.Use();
        _gridShader.SetCameraMatrices(_camera.GetViewMatrix(), _camera.GetProjectionMatrix(_viewportBuffer.Width, _viewportBuffer.Height));
        _gridShader.SetUniform("uColor", new Vector4(1.0f, 1.0f, 1.0f, opacity));

        var viewPos = _camera.Position;
        float viewWidth = _viewportBuffer.Width / _camera.Zoom;
        float viewHeight = _viewportBuffer.Height / _camera.Zoom;

        long minX = (long)Math.Floor((viewPos.X - viewWidth / 2) / gridSize) * gridSize;
        long maxX = (long)Math.Ceiling((viewPos.X + viewWidth / 2) / gridSize) * gridSize;
        long minY = (long)Math.Floor((viewPos.Y - viewHeight / 2) / gridSize) * gridSize;
        long maxY = (long)Math.Ceiling((viewPos.Y + viewHeight / 2) / gridSize) * gridSize;

        List<float> vertices = new List<float>();
        for (long x = minX; x <= maxX; x += gridSize)
        {
            vertices.Add(x); vertices.Add(minY); vertices.Add(0);
            vertices.Add(x); vertices.Add(maxY); vertices.Add(0);
        }
        for (long y = minY; y <= maxY; y += gridSize)
        {
            vertices.Add(minX); vertices.Add(y); vertices.Add(0);
            vertices.Add(maxX); vertices.Add(y); vertices.Add(0);
        }

        _gl.BindVertexArray(_gridVao);
        _gl.BindBuffer(BufferTargetARB.ArrayBuffer, _gridVbo);
        unsafe
        {
            fixed (float* v = vertices.ToArray())
            {
                _gl.BufferData(BufferTargetARB.ArrayBuffer, (nuint)(vertices.Count * sizeof(float)), v, BufferUsageARB.StreamDraw);
            }
            _gl.EnableVertexAttribArray(0);
            _gl.VertexAttribPointer(0, 3, VertexAttribPointerType.Float, false, 3 * sizeof(float), (void*)0);
        }

        _gl.DrawArrays(PrimitiveType.Lines, 0, (uint)(vertices.Count / 3));
        _gl.BindVertexArray(0);
    }

    public void RenderGizmos()
    {
        // TODO: Implement gizmo rendering
    }

    public void HighlightSelection(long entityId)
    {
        // TODO: Implement selection highlighting
    }

    public override void Dispose()
    {
        _worldRenderer?.Dispose();
        _viewportBuffer?.Dispose();
        _gridShader?.Dispose();
        if (_gl != null)
        {
            _gl.DeleteVertexArray(_gridVao);
            _gl.DeleteBuffer(_gridVbo);
        }
        base.Dispose();
    }
}
