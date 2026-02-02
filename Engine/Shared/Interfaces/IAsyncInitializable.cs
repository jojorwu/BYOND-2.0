using Shared.Models;
using Shared.Interfaces;
using Shared.Enums;
using Shared.Operations;
using Shared.Services;
using System.Threading.Tasks;

namespace Shared.Interfaces
{
    public interface IAsyncInitializable
    {
        Task InitializeAsync();
    }
}
