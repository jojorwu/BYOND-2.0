using Silk.NET.OpenGL;
using System.Numerics;
using Shared;
using Shared.Attributes;
using Shared.Services;
using Shared.Interfaces;

namespace Editor;

/// <summary>
/// Foundation for Editor-specific world rendering, grids, and gizmos.
/// </summary>
[EngineService]
public class EditorRenderer : EngineService
{
    private GL? _gl;
    private readonly IGameState _gameState;
    private readonly IArchetypeManager _archetypeManager;

    public EditorRenderer(IGameState gameState, IArchetypeManager archetypeManager)
    {
        _gameState = gameState;
        _archetypeManager = archetypeManager;
    }

    public void Initialize(GL gl)
    {
        _gl = gl;
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
}
