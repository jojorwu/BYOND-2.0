namespace Shared.Interfaces
{
    public interface IComponentMessageBus
    {
        void SendMessage(IGameObject target, IComponentMessage message);
        void BroadcastMessage(IComponentMessage message);
    }
}
