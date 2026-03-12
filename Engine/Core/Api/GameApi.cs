using Shared;
using System.Collections.Generic;
using Shared.Interfaces;
using System.Linq;

namespace Core.Api
{
    public class GameApi : IGameApi
    {
        private readonly IApiRegistry _registry;

        public IMapApi Map => _registry.Get<IMapApi>("Map");
        public IObjectApi Objects => _registry.Get<IObjectApi>("Objects");
        public IScriptApi Scripts => _registry.Get<IScriptApi>("Scripts");
        public ISoundApi Sounds => _registry.Get<ISoundApi>("Sounds");
        public Shared.Config.ISoundRegistry SoundRegistry { get; }
        public IStandardLibraryApi StdLib => _registry.Get<IStandardLibraryApi>("StandardLibrary");
        public Shared.Config.IConsoleCommandManager Commands { get; }
        public ITimeApi Time => _registry.Get<ITimeApi>("Time");
        public IEventApi Events => _registry.Get<IEventApi>("Events");

        public GameApi(
            IApiRegistry registry,
            Shared.Config.ISoundRegistry soundRegistry,
            Shared.Config.IConsoleCommandManager commandManager)
        {
            _registry = registry;
            SoundRegistry = soundRegistry;
            Commands = commandManager;
        }
    }
}
