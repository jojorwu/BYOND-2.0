namespace Editor
{
    public interface IEditorSettingsManager
    {
        EditorSettings Settings { get; }
        void Save();
    }
}
