namespace Shared
{
    /// <summary>
    /// Represents a compiled DM procedure or a native procedure callable from the VM.
    /// </summary>
    public interface IDreamProc
    {
        /// <summary>
        /// The unique name of the procedure.
        /// </summary>
        string Name { get; }
    }
}
