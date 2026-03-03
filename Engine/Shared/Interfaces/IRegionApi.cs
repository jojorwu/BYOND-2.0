namespace Shared;
    public interface IRegionApi
    {
        void SetRegionActive(long x, long y, long z, bool active);
        bool IsRegionActive(long x, long y, long z);
    }
