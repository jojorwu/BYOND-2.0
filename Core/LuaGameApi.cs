using System;
using System.Threading.Tasks;

namespace Core
{
    public class LuaGameApi
    {
        private readonly GameApi _gameApi;

        public LuaGameApi(GameApi gameApi)
        {
            _gameApi = gameApi;
        }

        public GameState GetState() => _gameApi.GetState();
        public Map? GetMap() => _gameApi.GetMap();
        public Turf? GetTurf(int x, int y, int z) => _gameApi.GetTurf(x, y, z);
        public void SetTurf(int x, int y, int z, int turfId) => _gameApi.SetTurf(x, y, z, turfId);
        public GameObject? CreateObject(string typeName, int x, int y, int z) => _gameApi.CreateObject(typeName, x, y, z);
        public GameObject? GetObject(int id) => _gameApi.GetObject(id);
        public void DestroyObject(int id) => _gameApi.DestroyObject(id);

        public void LoadMap(string filePath)
        {
            try
            {
                Task.Run(async () => await _gameApi.LoadMapAsync(filePath)).Wait();
            }
            catch (Exception e)
            {
                Console.WriteLine($"[ERROR] LuaGameApi.LoadMap failed: {e.Message}");
            }
        }

        public void SaveMap(string filePath)
        {
            try
            {
                Task.Run(async () => await _gameApi.SaveMapAsync(filePath)).Wait();
            }
            catch (Exception e)
            {
                Console.WriteLine($"[ERROR] LuaGameApi.SaveMap failed: {e.Message}");
            }
        }

        public System.Collections.Generic.List<string> ListScriptFiles() => _gameApi.ListScriptFiles();
        public bool ScriptFileExists(string filename) => _gameApi.ScriptFileExists(filename);
        public string ReadScriptFile(string filename) => _gameApi.ReadScriptFile(filename);
    }
}
