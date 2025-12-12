using System.Collections.Generic;

namespace Shared
{
    public interface IStandardLibraryApi
    {
        void Restart();
        IGameObject? Locate(string typePath, List<IGameObject> container);
        List<IGameObject> Range(float distance, int centerX, int centerY, int centerZ);
        List<IGameObject> View(int distance, IGameObject viewer);
    }
}
