using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Shared;
using Shared.Attributes;
using Shared.Services;
using Shared.Interfaces;
using Core;

namespace Editor;

/// <summary>
/// Maintains the global state of the Editor.
/// </summary>
[EngineService]
public class EditorState : EngineService
{
    public string? CurrentProjectPath { get; set; }
    public bool IsDirty { get; set; }

    // Selection state
    public long SelectedEntityId { get; set; } = -1;

    // Grid settings
    public int GridSize { get; set; } = 32;
    public bool SnapToGrid { get; set; } = true;
}

/// <summary>
/// Provides orchestration logic for Editor actions and workspace management.
/// </summary>
[EngineService]
public class EditorContext : EngineService
{
    private readonly EditorState _state;
    private readonly CommandHistory _history;
    private readonly IProjectManager _projectManager;
    private readonly IMapLoader _mapLoader;
    private readonly IGameState _gameState;
    private readonly ILogger<EditorContext> _logger;

    public CommandHistory History => _history;
    public EditorState State => _state;

    public EditorContext(
        EditorState state,
        CommandHistory history,
        IProjectManager projectManager,
        IMapLoader mapLoader,
        IGameState gameState,
        ILogger<EditorContext> logger)
    {
        _state = state;
        _history = history;
        _projectManager = projectManager;
        _mapLoader = mapLoader;
        _gameState = gameState;
        _logger = logger;
    }

    /// <summary>
    /// Asynchronously loads a project and its primary map.
    /// </summary>
    public async Task LoadProjectAsync(string path)
    {
        if (string.IsNullOrWhiteSpace(path)) throw new ArgumentException("Project path cannot be null or empty.", nameof(path));

        try
        {
            _state.CurrentProjectPath = path;
            var project = _projectManager.LoadProject(path);
            if (project != null)
            {
                var mapPath = project.GetFullPath(Constants.MapsRoot) + "/world.dmm";
                var map = await _mapLoader.LoadMapAsync(mapPath);
                if (map != null)
                {
                    _gameState.Map = map;
                    _state.IsDirty = false;
                    _logger.LogInformation("Project loaded successfully from: {Path}", path);
                }
                else
                {
                    _logger.LogWarning("Failed to load map from: {Path}", mapPath);
                }
            }
            else
            {
                _logger.LogError("Could not find project at: {Path}", path);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error while loading project: {Path}", path);
            throw;
        }
    }

    public async Task SaveProjectAsync()
    {
        if (_state.CurrentProjectPath == null) return;
        // TODO: Implement actual save logic via MapLoader
        _state.IsDirty = false;
        await Task.CompletedTask;
    }
}
