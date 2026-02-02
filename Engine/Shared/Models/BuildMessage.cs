using Shared.Models;
using Shared.Interfaces;
using Shared.Enums;
using Shared.Operations;
using Shared.Services;
namespace Shared.Models
{
    public record BuildMessage(string File, int Line, string Text, BuildMessageLevel Level);

    public enum BuildMessageLevel
    {
        Info,
        Warning,
        Error
    }
}
