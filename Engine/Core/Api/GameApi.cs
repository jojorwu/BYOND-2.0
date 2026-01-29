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
        public IStandardLibraryApi StdLib { get; }

        public GameApi(IMapApi mapApi, IObjectApi objectApi, IScriptApi scriptApi, IStandardLibraryApi standardLibraryApi)
        {
            Map = mapApi;
            Objects = objectApi;
            Scripts = scriptApi;
            StdLib = standardLibraryApi;
        }
    }
}
