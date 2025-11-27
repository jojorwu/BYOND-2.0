using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;

namespace Core
{
    /// <summary>
    /// Provides an API for scripts to interact with the game world.
    /// </summary>
    public class GameApi
    {
        private readonly Project _project;
        private readonly GameState _gameState;
        private readonly ObjectTypeManager _objectTypeManager;
        private readonly MapLoader _mapLoader;

        /// <summary>
        /// Initializes a new instance of the <see cref="GameApi"/> class.
        /// </summary>
        /// <param name="project">The project context.</param>
        /// <param name="gameState">The game state to interact with.</param>
        /// <param name="objectTypeManager">The object type manager.</param>
        public GameApi(Project project, GameState gameState, ObjectTypeManager objectTypeManager, MapLoader mapLoader)
        {
            _project = project;
            _gameState = gameState;
            _objectTypeManager = objectTypeManager;
            _mapLoader = mapLoader;
        }

        public GameState GetState()
        {
            return _gameState;
        }

        public Map? GetMap()
        {
            return _gameState.Map;
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

        public void MoveObject(int id, int x, int y, int z)
        {
            var gameObject = GetObject(id);
            if (gameObject != null)
            {
                var oldTurf = GetTurf(gameObject.X, gameObject.Y, gameObject.Z);
                if (oldTurf != null)
                {
                    oldTurf.Contents.Remove(gameObject);
                }

                gameObject.X = x;
                gameObject.Y = y;
                gameObject.Z = z;

                var newTurf = GetTurf(x, y, z);
                if (newTurf != null)
                {
                    newTurf.Contents.Add(gameObject);
                }
            }
        }

        /// <summary>
        /// Loads a map from a file.
        /// </summary>
        /// <param name="filePath">The path to the map file.</param>
        public void LoadMap(string filePath)
        {
            var safePath = SanitizePath(filePath, Constants.MapsRoot);
            _gameState.Map = _mapLoader.LoadMap(safePath);
        }

        /// <summary>
        /// Compiles and loads a map in the DMM format.
        /// This will also load all DM object types defined in the project.
        /// </summary>
        /// <param name="filePath">The path to the DMM map file.</param>
        public void LoadDmmMap(string filePath)
        {
            var safePath = SanitizePath(filePath, Constants.MapsRoot);

            // Find all DM and DMM files to compile
            var dmFiles = Directory.GetFiles(_project.GetFullPath(Constants.ScriptsRoot), "*.dm", SearchOption.AllDirectories).ToList();
            dmFiles.Add(safePath); // Add the map itself to the compilation list

            var compilerService = new OpenDreamCompilerService();
            var (jsonPath, messages) = compilerService.Compile(dmFiles);

            // Print compiler messages for debugging
            messages.ForEach(Console.WriteLine);

            if (jsonPath != null && File.Exists(jsonPath))
            {
                _objectTypeManager.Clear(); // Clear old types before loading new ones
                var loader = new DreamMakerLoader(_objectTypeManager);
                _gameState.Map = loader.Load(jsonPath);
            }
            else
            {
                throw new Exception("DMM compilation failed. See console for details.");
            }
        }

        /// <summary>
        /// Saves the current map to a file.
        /// </summary>
        /// <param name="filePath">The path to save the map file to.</param>
        public void SaveMap(string filePath)
        {
            if (_gameState.Map != null)
            {
                var safePath = SanitizePath(filePath, Constants.MapsRoot);
                _mapLoader.SaveMap(_gameState.Map, safePath);
            }
        }

        // --- Script Management Methods ---

        private string SanitizePath(string userProvidedPath, string expectedRootFolder)
        {
            // Get the full path of the project's root for the given type (e.g., /tmp/proj/scripts)
            var fullRootPath = Path.GetFullPath(_project.GetFullPath(expectedRootFolder));

            // Get the full path of the user-provided file relative to the project root
            var fullUserPath = Path.GetFullPath(_project.GetFullPath(userProvidedPath));


            if (!fullUserPath.StartsWith(fullRootPath))
            {
                throw new System.Security.SecurityException("Access to path is denied.");
            }
            return fullUserPath;
        }

        /// <summary>
        /// Lists all script files in the scripts directory.
        /// </summary>
        /// <returns>A list of script file paths.</returns>
        public List<string> ListScriptFiles()
        {
            var rootPath = Path.GetFullPath(Constants.ScriptsRoot);
            if (!Directory.Exists(rootPath))
                return new List<string>();

            return Directory.GetFiles(rootPath, "*.lua", SearchOption.AllDirectories)
                .Select(path => Path.GetRelativePath(rootPath, path))
                .ToList();
        }

        /// <summary>
        /// Checks if a script file exists.
        /// </summary>
        /// <param name="filename">The name of the script file.</param>
        /// <returns>True if the file exists, false otherwise.</returns>
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

        /// <summary>
        /// Reads the content of a script file.
        /// </summary>
        /// <param name="filename">The name of the script file.</param>
        /// <returns>The content of the script file.</returns>
        public string ReadScriptFile(string filename)
        {
            var safePath = SanitizePath(filename, Constants.ScriptsRoot);
            return File.ReadAllText(safePath);
        }

        /// <summary>
        /// Writes content to a script file.
        /// </summary>
        /// <param name="filename">The name of the script file.</param>
        /// <param name="content">The content to write to the file.</param>
        public void WriteScriptFile(string filename, string content)
        {
            var safePath = SanitizePath(filename, Constants.ScriptsRoot);
            File.WriteAllText(safePath, content);
        }

        /// <summary>
        /// Deletes a script file.
        /// </summary>
        /// <param name="filename">The name of the script file to delete.</param>
        public void DeleteScriptFile(string filename)
        {
            var safePath = SanitizePath(filename, Constants.ScriptsRoot);
            File.Delete(safePath);
        }

        // --- DM Standard Library Functions ---

        public GameObject? Locate(string typePath, List<GameObject> container)
        {
            var targetType = _objectTypeManager.GetObjectType(typePath);
            if (targetType == null) return null;

            foreach (var obj in container)
            {
                var currentType = obj.ObjectType;
                while (currentType != null)
                {
                    if (currentType == targetType)
                    {
                        return obj;
                    }
                    if (currentType.ParentName == null) break;
                    currentType = _objectTypeManager.GetObjectType(currentType.ParentName);
                }
            }

            return null;
        }

        public void Sleep(int milliseconds)
        {
            Console.WriteLine($"[Warning] Game:Sleep({milliseconds}) is a blocking operation and will freeze the server thread. Use with caution.");
            Thread.Sleep(milliseconds);
        }

        public List<GameObject> Range(int distance, int centerX, int centerY, int centerZ)
        {
            var results = new List<GameObject>();
            foreach (var obj in _gameState.GameObjects.Values)
            {
                if (GetDistance(obj.X, obj.Y, obj.Z, centerX, centerY, centerZ) <= distance)
                {
                    results.Add(obj);
                }
            }
            return results;
        }

        public List<GameObject> View(int distance, GameObject viewer)
        {
            var results = new List<GameObject>();
            foreach (var obj in _gameState.GameObjects.Values)
            {
                if (obj == viewer) continue; // Can't see yourself

                if (GetDistance(viewer, obj) <= distance)
                {
                    if (HasLineOfSight(viewer, obj))
                    {
                        results.Add(obj);
                    }
                }
            }
            return results;
        }

        // --- Helper Methods ---

        private bool HasLineOfSight(GameObject from, GameObject to)
        {
            int x0 = from.X, y0 = from.Y;
            int x1 = to.X, y1 = to.Y;

            int dx = Math.Abs(x1 - x0);
            int sx = x0 < x1 ? 1 : -1;
            int dy = -Math.Abs(y1 - y0);
            int sy = y0 < y1 ? 1 : -1;
            int err = dx + dy;

            while (true)
            {
                // Check the turf at the current point for obstructions
                var turf = GetTurf(x0, y0, from.Z);
                if (turf != null)
                {
                    foreach (var content in turf.Contents)
                    {
                        // Don't check the start and end points for opacity
                        if (content != from && content != to)
                        {
                            if (content.GetProperty<int>("opacity") == 1)
                            {
                                return false; // Blocked
                            }
                        }
                    }
                }

                if (x0 == x1 && y0 == y1) break;

                int e2 = 2 * err;
                if (e2 >= dy)
                {
                    err += dy;
                    x0 += sx;
                }
                if (e2 <= dx)
                {
                    err += dx;
                    y0 += sy;
                }
            }

            return true; // No obstructions
        }

        private double GetDistance(GameObject a, GameObject b)
        {
            return GetDistance(a.X, a.Y, a.Z, b.X, b.Y, b.Z);
        }

        private double GetDistance(int x1, int y1, int z1, int x2, int y2, int z2)
        {
            var dx = x1 - x2;
            var dy = y1 - y2;
            var dz = z1 - z2;
            return Math.Sqrt(dx * dx + dy * dy + dz * dz);
        }
    }
}
