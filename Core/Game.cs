using Shared;

namespace Core
{
    public class Game : IGame
    {
        public IGameApi Api { get; }
        public IProject Project { get; }
        public IObjectTypeManager ObjectTypeManager { get; }
        public IDmmService DmmService { get; }

        public Game(IGameApi api, IProject project, IObjectTypeManager objectTypeManager, IDmmService dmmService)
        {
            Api = api;
            Project = project;
            ObjectTypeManager = objectTypeManager;
            DmmService = dmmService;
        }
    }
}
