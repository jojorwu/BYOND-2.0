using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using Robust.Shared.Maths;

namespace Shared;
    public interface IRegionManager
    {
        void Initialize();
        IEnumerable<Region> GetRegions(int z);
        bool TryGetRegion(int z, (long X, long Y) coords, [NotNullWhen(true)] out Region? region);
    }
