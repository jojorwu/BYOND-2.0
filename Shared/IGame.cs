namespace Core
{
    public interface IGame
    {
        void LoadProject(string projectPath);
        IGameApi Api { get; }
        IProject Project { get; }
        IObjectTypeManager ObjectTypeManager { get; }
        IDmmService DmmService { get; }
    }
}
