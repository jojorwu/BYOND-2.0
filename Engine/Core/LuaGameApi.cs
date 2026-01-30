using Shared;
using System;
using System.Threading.Tasks;

namespace Core
{
    public class LuaGameApi
    {
        private readonly IGameApi _gameApi;

        public LuaGameApi(IGameApi gameApi)
        {
            _gameApi = gameApi;
        }

        public IMap? GetMap() => _gameApi.Map.GetMap();
        public ITurf? GetTurf(int x, int y, int z) => _gameApi.Map.GetTurf(x, y, z);
        public void SetTurf(int x, int y, int z, int turfId) => _gameApi.Map.SetTurf(x, y, z, turfId);
        public GameObject? CreateObject(int typeId, int x, int y, int z) => _gameApi.Objects.CreateObject(typeId, x, y, z);
        public GameObject? GetObject(int id) => _gameApi.Objects.GetObject(id);
        public void DestroyObject(int id) => _gameApi.Objects.DestroyObject(id);

        public void LoadMap(string filePath)
        {
            try
            {
                Task.Run(async () => await _gameApi.Map.LoadMapAsync(filePath)).Wait();
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
                Task.Run(async () => await _gameApi.Map.SaveMapAsync(filePath)).Wait();
            }
            catch (Exception e)
            {
                Console.WriteLine($"[ERROR] LuaGameApi.SaveMap failed: {e.Message}");
            }
        }

        public System.Collections.Generic.List<string> ListScriptFiles() => _gameApi.Scripts.ListScriptFiles();
        public bool ScriptFileExists(string filename) => _gameApi.Scripts.ScriptFileExists(filename);
        public string ReadScriptFile(string filename) => _gameApi.Scripts.ReadScriptFile(filename);
    }
}
