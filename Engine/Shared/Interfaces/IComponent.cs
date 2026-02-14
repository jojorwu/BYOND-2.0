namespace Shared.Interfaces
{
    public interface IComponent
    {
        IGameObject? Owner { get; set; }
        bool Enabled { get; set; }
        void Initialize() { }
        void Shutdown() { }
    }
}
