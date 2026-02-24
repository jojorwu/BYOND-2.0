namespace Shared.Interfaces;
    /// <summary>
    /// Represents a message sent between components of the same entity.
    /// </summary>
    public interface IComponentMessage
    {
        /// <summary>
        /// Optional list of component types that should receive this message.
        /// If null or empty, the message is broadcast to all components.
        /// </summary>
        System.Type[]? TargetComponentTypes => null;
    }
