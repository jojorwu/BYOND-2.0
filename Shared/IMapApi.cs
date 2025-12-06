using System.Threading.Tasks;

namespace Shared
{
    public interface IMapApi
    {
        IMap? GetMap();
        ITurf? GetTurf(int x, int y, int z);
        void SetTurf(int x, int y, int z, int turfId);
        Task<IMap?> LoadMapAsync(string filePath);
        void SetMap(IMap map);
        Task SaveMapAsync(string filePath);
    }
}
