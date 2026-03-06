using Shared;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace Core.Api
{
    public class GameApi : IGameApi
    {
        public IMapApi Map { get; }
        public IObjectApi Objects { get; }
        public IScriptApi Scripts { get; }
        public ISoundApi Sounds { get; }
        public Shared.Config.ISoundRegistry SoundRegistry { get; }
        public IStandardLibraryApi StdLib { get; }
        public Shared.Config.IConsoleCommandManager Commands { get; }
        public ITimeApi Time { get; }
        public IEventApi Events { get; }

        public GameApi(IMapApi mapApi, IObjectApi objectApi, IScriptApi scriptApi, ISoundApi soundApi, Shared.Config.ISoundRegistry soundRegistry, IStandardLibraryApi standardLibraryApi, Shared.Config.IConsoleCommandManager commandManager, ITimeApi timeApi, IEventApi eventApi)
        {
            Map = mapApi;
            Objects = objectApi;
            Scripts = scriptApi;
            Sounds = soundApi;
            SoundRegistry = soundRegistry;
            StdLib = standardLibraryApi;
            Commands = commandManager;
            Time = timeApi;
            Events = eventApi;
        }
    }
}
