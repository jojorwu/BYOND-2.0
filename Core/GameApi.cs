using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;

namespace Core
{
    public class GameApi : IMapApi, IObjectApi, IStandardLibraryApi
    {
        private readonly IMapApi _mapApi;
        private readonly IObjectApi _objectApi;
        private readonly IStandardLibraryApi _standardLibraryApi;
        private readonly Project _project;
        private readonly GameState _gameState;

        public GameApi(IMapApi mapApi, IObjectApi objectApi, IStandardLibraryApi standardLibraryApi, Project project, GameState gameState)
        {
            _mapApi = mapApi;
            _objectApi = objectApi;
            _standardLibraryApi = standardLibraryApi;
            _project = project;
            _gameState = gameState;
        }

        // IMapApi
        public Map? GetMap() => _mapApi.GetMap();
        public Turf? GetTurf(int x, int y, int z) => _mapApi.GetTurf(x, y, z);
        public void SetTurf(int x, int y, int z, int turfId) => _mapApi.SetTurf(x, y, z, turfId);
        public Task LoadMapAsync(string filePath) => _mapApi.LoadMapAsync(filePath);
        public void SetMap(Map map) => _mapApi.SetMap(map);
        public Task SaveMapAsync(string filePath) => _mapApi.SaveMapAsync(filePath);

        // IObjectApi
        public GameObject? CreateObject(string typeName, int x, int y, int z) => _objectApi.CreateObject(typeName, x, y, z);
        public GameObject? GetObject(int id) => _objectApi.GetObject(id);
        public void DestroyObject(int id) => _objectApi.DestroyObject(id);
        public void MoveObject(int id, int x, int y, int z) => _objectApi.MoveObject(id, x, y, z);

        // IStandardLibraryApi
        public GameObject? Locate(string typePath, List<GameObject> container) => _standardLibraryApi.Locate(typePath, container);
        public void Sleep(int milliseconds) => _standardLibraryApi.Sleep(milliseconds);
        public List<GameObject> Range(int distance, int centerX, int centerY, int centerZ) => _standardLibraryApi.Range(distance, centerX, centerY, centerZ);
        public List<GameObject> View(int distance, GameObject viewer) => _standardLibraryApi.View(distance, viewer);

        public GameState GetState() => _gameState;

        // --- Script Management Methods ---

        private string SanitizePath(string userProvidedPath, string expectedRootFolder)
        {
            var fullRootPath = Path.GetFullPath(_project.GetFullPath(expectedRootFolder));
            var fullUserPath = Path.GetFullPath(_project.GetFullPath(userProvidedPath));

            if (!fullUserPath.StartsWith(fullRootPath))
            {
                throw new System.Security.SecurityException("Access to path is denied.");
            }
            return fullUserPath;
        }

        public List<string> ListScriptFiles()
        {
            var rootPath = Path.GetFullPath(Constants.ScriptsRoot);
            if (!Directory.Exists(rootPath))
                return new List<string>();

            return Directory.GetFiles(rootPath, "*.lua", SearchOption.AllDirectories)
                .Select(path => Path.GetRelativePath(rootPath, path))
                .ToList();
        }

        public bool ScriptFileExists(string filename)
        {
            try
            {
                var safePath = SanitizePath(filename, Constants.ScriptsRoot);
                return File.Exists(safePath);
            }
            catch (System.Security.SecurityException)
            {
                return false;
            }
        }

        public string ReadScriptFile(string filename)
        {
            var safePath = SanitizePath(filename, Constants.ScriptsRoot);
            return File.ReadAllText(safePath);
        }

        public void WriteScriptFile(string filename, string content)
        {
            var safePath = SanitizePath(filename, Constants.ScriptsRoot);
            File.WriteAllText(safePath, content);
        }

        public void DeleteScriptFile(string filename)
        {
            var safePath = SanitizePath(filename, Constants.ScriptsRoot);
            File.Delete(safePath);
        }
    }
}
