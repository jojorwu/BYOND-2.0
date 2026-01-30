using System.Threading.Tasks;

namespace Shared.Interfaces
{
    public interface IAsyncInitializable
    {
        Task InitializeAsync();
    }
}
