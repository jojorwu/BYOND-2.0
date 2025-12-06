using System.Collections.Generic;
using System.Threading.Tasks;

namespace Core
{
    public interface IGameApi
    {
        IMapApi Map { get; }
        IObjectApi Objects { get; }
        IScriptApi Scripts { get; }
        IStandardLibraryApi StdLib { get; }
    }
}
