namespace Shared
{
    public interface IRegionApi
    {
        void SetRegionActive(int x, int y, int z, bool active);
        bool IsRegionActive(int x, int y, int z);
    }
}
