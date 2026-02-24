using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using Robust.Shared.Maths;

namespace Shared;
    public interface IRegionManager
    {
        void Initialize();
        IEnumerable<Region> GetRegions(int z);
        bool TryGetRegion(int z, Vector2i coords, [NotNullWhen(true)] out Region? region);
    }
