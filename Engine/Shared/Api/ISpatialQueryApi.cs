using System.Collections.Generic;

namespace Shared.Api;
    public interface ISpatialQueryApi
    {
        List<GameObject> Range(int distance, int centerX, int centerY, int centerZ);
        List<GameObject> View(int distance, GameObject viewer);
        GameObject? Locate(string typePath, List<GameObject> container);
    }
