using System.Collections.Generic;

namespace Shared
{
    public interface IStandardLibraryApi
    {
        void Restart();
        GameObject? Locate(string typePath, List<GameObject> container);
        List<GameObject> Range(int distance, int centerX, int centerY, int centerZ);
        List<GameObject> View(int distance, GameObject viewer);
    }
}
