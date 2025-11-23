using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace Core
{
    /// <summary>
    /// Provides an API for scripts to interact with the game world.
    /// </summary>
    public class GameApi
    {
        private readonly GameState _gameState;
        private readonly ObjectTypeManager _objectTypeManager;
        private readonly MapLoader _mapLoader;

        /// <summary>
        /// Initializes a new instance of the <see cref="GameApi"/> class.
        /// </summary>
        /// <param name="gameState">The game state to interact with.</param>
        /// <param name="objectTypeManager">The object type manager.</param>
        public GameApi(GameState gameState, ObjectTypeManager objectTypeManager)
        {
            _gameState = gameState;
            _objectTypeManager = objectTypeManager;
            _mapLoader = new MapLoader(_objectTypeManager);
        }

        // --- Map and Object Methods ---

        /// <summary>
        /// Creates a new map with the specified dimensions.
        /// </summary>
        /// <param name="width">The width of the map.</param>
        /// <param name="height">The height of the map.</param>
        /// <param name="depth">The depth of the map.</param>
        public void CreateMap(int width, int height, int depth)
        {
            _gameState.Map = new Map(width, height, depth);
        }

        /// <summary>
        /// Gets the turf at the specified coordinates.
        /// </summary>
        /// <param name="x">The X-coordinate.</param>
        /// <param name="y">The Y-coordinate.</param>
        /// <param name="z">The Z-coordinate.</param>
        /// <returns>The turf at the specified coordinates, or null if the coordinates are out of bounds.</returns>
        public Turf? GetTurf(int x, int y, int z)
        {
            return _gameState.Map?.GetTurf(x, y, z);
        }

        /// <summary>
        /// Sets the turf at the specified coordinates.
        /// </summary>
        /// <param name="x">The X-coordinate.</param>
        /// <param name="y">The Y-coordinate.</param>
        /// <param name="z">The Z-coordinate.</param>
        /// <param name="turfId">The identifier of the turf type to set.</param>
        public void SetTurf(int x, int y, int z, int turfId)
        {
            _gameState.Map?.SetTurf(x, y, z, new Turf(turfId));
        }

        /// <summary>
        /// Creates a new game object at the specified coordinates.
        /// </summary>
        /// <param name="typeName">The name of the object type.</param>
        /// <param name="x">The X-coordinate.</param>
        /// <param name="y">The Y-coordinate.</param>
        /// <param name="z">The Z-coordinate.</param>
        /// <returns>The newly created game object, or null if the object type does not exist.</returns>
        public GameObject? CreateObject(string typeName, int x, int y, int z)
        {
            var objectType = _objectTypeManager.GetObjectType(typeName);
            if (objectType == null)
            {
                return null;
            }

            var gameObject = new GameObject(objectType, x, y, z);
            _gameState.GameObjects.Add(gameObject.Id, gameObject);
            var turf = GetTurf(x, y, z);
            if (turf != null)
            {
                turf.Contents.Add(gameObject);
            }
            return gameObject;
        }

        /// <summary>
        /// Gets a game object by its unique identifier.
        /// </summary>
        /// <param name="id">The unique identifier of the game object.</param>
        /// <returns>The game object with the specified identifier, or null if no such object exists.</returns>
        public GameObject? GetObject(int id)
        {
            _gameState.GameObjects.TryGetValue(id, out var obj);
            return obj;
        }

        /// <summary>
        /// Destroys a game object, removing it from the game state and its turf.
        /// </summary>
        /// <param name="id">The unique identifier of the game object to destroy.</param>
        public void DestroyObject(int id)
        {
            var gameObject = GetObject(id);
            if (gameObject != null)
            {
                var turf = GetTurf(gameObject.X, gameObject.Y, gameObject.Z);
                if (turf != null)
                {
                    turf.Contents.Remove(gameObject);
                }
                _gameState.GameObjects.Remove(id);
            }
        }

        /// <summary>
        /// Loads a map from a file.
        /// </summary>
        /// <param name="filePath">The path to the map file.</param>
        public void LoadMap(string filePath)
        {
            _gameState.Map = _mapLoader.LoadMap(filePath);
        }

        /// <summary>
        /// Saves the current map to a file.
        /// </summary>
        /// <param name="filePath">The path to save the map file to.</param>
        public void SaveMap(string filePath)
        {
            if (_gameState.Map != null)
            {
                _mapLoader.SaveMap(_gameState.Map, filePath);
            }
        }

        public void SetObjectProperty(int objectId, string key, object value)
        {
            var obj = GetObject(objectId);
            if (obj != null)
            {
                obj.Properties[key] = value;
            }
        }

        public object? GetObjectProperty(int objectId, string key)
        {
            var obj = GetObject(objectId);
            return obj?.GetProperty<object>(key);
        }

        // --- Script Management Methods ---

        private string SanitizePath(string filename)
        {
            // Prevent directory traversal attacks
            var fullPath = Path.GetFullPath(Path.Combine(Constants.ScriptsRoot, filename));
            var rootPath = Path.GetFullPath(Constants.ScriptsRoot);

            if (!fullPath.StartsWith(rootPath))
            {
                throw new System.Security.SecurityException("Access to path is denied.");
            }
            return fullPath;
        }

        /// <summary>
        /// Lists all script files in the scripts directory.
        /// </summary>
        /// <returns>A list of script file paths.</returns>
        public List<string> ListScriptFiles()
        {
            var rootPath = Path.GetFullPath(Constants.ScriptsRoot);
            return Directory.GetFiles(rootPath, "*.lua", SearchOption.AllDirectories)
                .Select(path => Path.GetRelativePath(rootPath, path))
                .ToList();
        }

        /// <summary>
        /// Reads the content of a script file.
        /// </summary>
        /// <param name="filename">The name of the script file.</param>
        /// <returns>The content of the script file.</returns>
        public string ReadScriptFile(string filename)
        {
            var safePath = SanitizePath(filename);
            return File.ReadAllText(safePath);
        }

        /// <summary>
        /// Writes content to a script file.
        /// </summary>
        /// <param name="filename">The name of the script file.</param>
        /// <param name="content">The content to write to the file.</param>
        public void WriteScriptFile(string filename, string content)
        {
            var safePath = SanitizePath(filename);
            File.WriteAllText(safePath, content);
        }

        /// <summary>
        /// Deletes a script file.
        /// </summary>
        /// <param name="filename">The name of the script file to delete.</param>
        public void DeleteScriptFile(string filename)
        {
            var safePath = SanitizePath(filename);
            File.Delete(safePath);
        }
    }
}
