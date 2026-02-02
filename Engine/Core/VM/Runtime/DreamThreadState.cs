using Shared.Models;
using Shared.Interfaces;
using Shared.Enums;
using Shared.Operations;
using Shared.Services;
namespace Core.VM.Runtime
{
    public enum DreamThreadState
    {
        Running,
        Sleeping,
        Finished,
        Error
    }
}
