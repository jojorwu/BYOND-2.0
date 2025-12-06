namespace Shared
{
    public interface IGame
    {
        IGameApi Api { get; }
        IProject Project { get; }
        IObjectTypeManager ObjectTypeManager { get; }
        IDmmService DmmService { get; }
    }
}
