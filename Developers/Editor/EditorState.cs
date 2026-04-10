using Shared.Attributes;
using Shared.Services;

namespace Editor
{
    [EngineService]
    public class EditorState : EngineService
    {
        public string? CurrentProjectPath { get; set; }
        public bool IsDirty { get; set; }

        // Selection state
        public long SelectedEntityId { get; set; } = -1;
    }

    [EngineService]
    public class EditorContext : EngineService
    {
        private readonly EditorState _state;

        public EditorContext(EditorState state)
        {
            _state = state;
        }

        // Business logic for editor actions
        public void LoadProject(string path)
        {
            _state.CurrentProjectPath = path;
            _state.IsDirty = false;
        }
    }
}
