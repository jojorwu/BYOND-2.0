using System.Threading.Tasks;

namespace Core
{
    public interface IMapApi
    {
        Map? GetMap();
        Turf? GetTurf(int x, int y, int z);
        void SetTurf(int x, int y, int z, int turfId);
        Task LoadMapAsync(string filePath);
        Task SaveMapAsync(string filePath);
        void SetMap(Map map);
    }
}
