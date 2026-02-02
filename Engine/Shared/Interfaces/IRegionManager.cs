using Shared.Models;
using Shared.Interfaces;
using Shared.Enums;
using Shared.Operations;
using Shared.Services;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using Robust.Shared.Maths;

namespace Shared.Interfaces
{
    public interface IRegionManager
    {
        void Initialize();
        IEnumerable<Region> GetRegions(int z);
        bool TryGetRegion(int z, Vector2i coords, [NotNullWhen(true)] out Region? region);
    }
}
