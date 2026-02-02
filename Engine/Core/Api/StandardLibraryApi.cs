using Shared;
using Shared.Models;
using Shared.Services;
using Shared.Interfaces;
using Shared.Enums;
using Shared.Operations;
using System;
using System.Collections.Generic;
using System.Threading;
using Shared.Api;

namespace Core.Api
{
    public class StandardLibraryApi : IStandardLibraryApi
    {
        private readonly ISpatialQueryApi _spatialQueryApi;

        public StandardLibraryApi(ISpatialQueryApi spatialQueryApi)
        {
            _spatialQueryApi = spatialQueryApi;
        }

        public GameObject? Locate(string typePath, List<GameObject> container)
        {
            return _spatialQueryApi.Locate(typePath, container);
        }

        public void Sleep(int milliseconds)
        {
            Console.WriteLine($"[Warning] Game:Sleep({milliseconds}) is a blocking operation and will freeze the server thread. Use with caution.");
            Thread.Sleep(milliseconds);
        }

        public List<GameObject> Range(int distance, int centerX, int centerY, int centerZ)
        {
            return _spatialQueryApi.Range(distance, centerX, centerY, centerZ);
        }

        public List<GameObject> View(int distance, GameObject viewer)
        {
            return _spatialQueryApi.View(distance, viewer);
        }
    }
}
