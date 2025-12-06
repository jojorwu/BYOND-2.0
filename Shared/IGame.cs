namespace Shared
{
    public interface IGame
    {
        void LoadProject(IProject project);
        IGameApi Api { get; }
        IProject Project { get; }
        IObjectTypeManager ObjectTypeManager { get; }
        IDmmService DmmService { get; }
    }
}
