namespace Core
{
    /// <summary>
    /// Provides an API for scripts to interact with the editor.
    /// </summary>
    public class EditorApi
    {
        /// <summary>
        /// Gets the current state of the editor.
        /// </summary>
        public EditorState State { get; }

        /// <summary>
        /// Gets the manager for object selection.
        /// </summary>
        public SelectionManager Selection { get; }

        /// <summary>
        /// Gets the asset browser.
        /// </summary>
        public AssetBrowser Assets { get; }

        /// <summary>
        /// Initializes a new instance of the <see cref="EditorApi"/> class.
        /// </summary>
        public EditorApi()
        {
            State = new EditorState();
            Selection = new SelectionManager();
            Assets = new AssetBrowser(new AssetManager());
        }
    }
}
