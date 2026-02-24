using System.Threading.Tasks;

namespace Shared;
    public interface IDmmService
    {
        Task<IMap?> LoadMapAsync(string path);
    }
