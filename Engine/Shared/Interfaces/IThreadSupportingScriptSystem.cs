using Shared.Models;
using Shared.Interfaces;
using Shared.Enums;
using Shared.Operations;
using Shared.Services;
namespace Shared.Interfaces
{
    public interface IThreadSupportingScriptSystem : IScriptSystem
    {
        IScriptThread? CreateThread(string procName, IGameObject? associatedObject = null);
    }
}
