using System.Collections.Generic;

namespace Core
{
    public interface IStandardLibraryApi
    {
        GameObject? Locate(string typePath, List<GameObject> container);
        void Sleep(int milliseconds);
        List<GameObject> Range(int distance, int centerX, int centerY, int centerZ);
        List<GameObject> View(int distance, GameObject viewer);
    }
}
