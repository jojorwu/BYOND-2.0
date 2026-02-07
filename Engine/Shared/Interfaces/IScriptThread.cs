namespace Shared
{
    /// <summary>
    /// Represents an independently executing sequence of script instructions.
    /// </summary>
    public interface IScriptThread
    {
        /// <summary>
        /// The game object this thread is associated with, if any.
        /// </summary>
        IGameObject? AssociatedObject { get; }
    }
}
