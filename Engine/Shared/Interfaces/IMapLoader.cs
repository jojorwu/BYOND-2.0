using System.Threading.Tasks;

namespace Shared;
    public interface IMapLoader
    {
        Task<IMap?> LoadMapAsync(string filePath);
        Task SaveMapAsync(IMap map, string filePath);
    }
