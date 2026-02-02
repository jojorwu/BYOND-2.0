using Shared.Models;
using Shared.Interfaces;
using Shared.Enums;
using Shared.Operations;
using Shared.Services;
namespace Editor
{
    public interface IEditorSettingsManager
    {
        EditorSettings Settings { get; }
        void Save();
    }
}
