namespace Core
{
    /// <summary>
    /// Represents the available tools in the editor.
    /// </summary>
    public enum EditorTool
    {
        /// <summary>
        /// The selection tool for manipulating objects.
        /// </summary>
        Selection,

        /// <summary>
        /// The eyedropper tool for selecting assets from the map.
        /// </summary>
        Eyedropper
    }

    /// <summary>
    /// Manages the overall state of the editor.
    /// </summary>
    public class EditorState
    {
        /// <summary>
        /// Gets or sets the currently selected editor tool.
        /// </summary>
        public EditorTool CurrentTool { get; set; } = EditorTool.Selection;
    }
}
