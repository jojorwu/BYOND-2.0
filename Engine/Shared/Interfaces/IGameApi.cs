using System.Collections.Generic;
using System.Threading.Tasks;

namespace Shared;
    public interface IGameApi
    {
        IMapApi Map { get; }
        IObjectApi Objects { get; }
        IScriptApi Scripts { get; }
        ISoundApi Sounds { get; }
        Shared.Config.ISoundRegistry SoundRegistry { get; }
        IStandardLibraryApi StdLib { get; }
        Shared.Config.IConsoleCommandManager Commands { get; }
        ITimeApi Time { get; }
        IEventApi Events { get; }
    }
