using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace Core
{
    public class GameApi
    {
        private readonly GameState gameState;
        private const string ScriptsRoot = "scripts";

        public GameApi(GameState gameState)
        {
            this.gameState = gameState;
        }

        // --- Map and Object Methods ---
        public void CreateMap(int width, int height, int depth)
        {
            gameState.Map = new Map(width, height, depth);
        }

        public Tile GetTile(int x, int y, int z)
        {
            return gameState.Map?.GetTile(x, y, z);
        }

        public void SetTile(int x, int y, int z, int tileId)
        {
            gameState.Map?.SetTile(x, y, z, new Tile(tileId));
        }

        public GameObject CreateObject(string name, int x, int y, int z)
        {
            var gameObject = new GameObject(name, x, y, z);
            gameState.GameObjects.Add(gameObject);
            return gameObject;
        }

        public GameObject GetObject(int id)
        {
            return gameState.GameObjects.FirstOrDefault(obj => obj.Id == id);
        }

        public void LoadMap(string filePath)
        {
            gameState.Map = MapLoader.LoadMap(filePath);
        }

        public void SaveMap(string filePath)
        {
            if (gameState.Map != null)
            {
                MapLoader.SaveMap(gameState.Map, filePath);
            }
        }

        // --- Script Management Methods ---

        private string SanitizePath(string filename)
        {
            // Prevent directory traversal attacks
            var fullPath = Path.GetFullPath(Path.Combine(ScriptsRoot, filename));
            var rootPath = Path.GetFullPath(ScriptsRoot);

            if (!fullPath.StartsWith(rootPath))
            {
                throw new System.Security.SecurityException("Access to path is denied.");
            }
            return fullPath;
        }

        public List<string> ListScriptFiles()
        {
            var rootPath = Path.GetFullPath(ScriptsRoot);
            return Directory.GetFiles(rootPath, "*.lua", SearchOption.AllDirectories)
                .Select(path => Path.GetRelativePath(rootPath, path))
                .ToList();
        }

        public string ReadScriptFile(string filename)
        {
            var safePath = SanitizePath(filename);
            return File.ReadAllText(safePath);
        }

        public void WriteScriptFile(string filename, string content)
        {
            var safePath = SanitizePath(filename);
            File.WriteAllText(safePath, content);
        }

        public void DeleteScriptFile(string filename)
        {
            var safePath = SanitizePath(filename);
            File.Delete(safePath);
        }
    }
}
