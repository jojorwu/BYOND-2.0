using Shared.Models;
using Shared.Interfaces;
using Shared.Enums;
using Shared.Operations;
using Shared.Services;
namespace Shared.Interfaces
{
    public interface IRegionApi
    {
        void SetRegionActive(int x, int y, int z, bool active);
        bool IsRegionActive(int x, int y, int z);
    }
}
