using System.Collections.Generic;
using System.Threading.Tasks;

namespace Shared;
    public interface IGameApi
    {
        IMapApi Map { get; }
        IObjectApi Objects { get; }
        IScriptApi Scripts { get; }
        ISoundApi Sounds { get; }
        IStandardLibraryApi StdLib { get; }
    }
