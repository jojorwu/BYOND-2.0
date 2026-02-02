using Shared.Models;
using Shared.Interfaces;
using Shared.Enums;
using Shared.Operations;
using Shared.Services;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace Shared.Interfaces
{
    public interface IGameApi
    {
        IMapApi Map { get; }
        IObjectApi Objects { get; }
        IScriptApi Scripts { get; }
        IStandardLibraryApi StdLib { get; }
    }
}
