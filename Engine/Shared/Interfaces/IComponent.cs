namespace Shared.Interfaces;
    public interface IComponent : IPoolable
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

        /// <summary>
        /// Prepares the component for a new update cycle by copying current state to the next state buffer.
        /// </summary>
        void BeginUpdate() { }

        /// <summary>
        /// Finalizes the update cycle by swapping current state with the updated next state buffer.
        /// </summary>
        void CommitUpdate() { }
    }
