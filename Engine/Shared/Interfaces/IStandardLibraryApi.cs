using Shared.Models;
using Shared.Interfaces;
using Shared.Enums;
using Shared.Operations;
using Shared.Services;
using System.Collections.Generic;

namespace Shared.Interfaces
{
    public interface IStandardLibraryApi
    {
        GameObject? Locate(string typePath, List<GameObject> container);
        void Sleep(int milliseconds);
        List<GameObject> Range(int distance, int centerX, int centerY, int centerZ);
        List<GameObject> View(int distance, GameObject viewer);
    }
}
