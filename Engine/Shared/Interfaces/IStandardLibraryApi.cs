using System.Collections.Generic;

namespace Shared;
    public interface IStandardLibraryApi
    {
        GameObject? Locate(string typePath, List<GameObject> container);
        void Sleep(int milliseconds);
        List<GameObject> Range(int distance, int centerX, int centerY, int centerZ);
        List<GameObject> View(int distance, GameObject viewer);
        int Step(GameObject obj, int dir, int speed);
        int StepTo(GameObject obj, GameObject target, int minDist, int speed);
    }
