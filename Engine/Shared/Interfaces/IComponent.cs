namespace Shared.Interfaces
{
    public interface IComponent
    {
        IGameObject? Owner { get; set; }
        bool Enabled { get; set; }
        void Initialize() { }
        void Shutdown() { }

        /// <summary>
        /// Sends a message to other components of the same entity.
        /// </summary>
        void SendMessage(IComponentMessage message);

        /// <summary>
        /// Called when a message is received from another component of the same entity.
        /// </summary>
        void OnMessage(IComponentMessage message) { }
    }
}
