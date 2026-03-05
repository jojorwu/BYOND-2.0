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
        public ITurf? GetTurf(long x, long y, long z) => _gameApi.Map.GetTurf(x, y, z);
        public void SetTurf(long x, long y, long z, int turfId) => _gameApi.Map.SetTurf(x, y, z, turfId);
        public GameObject? CreateObject(int typeId, long x, long y, long z) => _gameApi.Objects.CreateObject(typeId, x, y, z);
        public GameObject? GetObject(long id) => _gameApi.Objects.GetObject(id);
        public void DestroyObject(long id) => _gameApi.Objects.DestroyObject(id);

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

        public void PlaySound(string file, float volume = 100f, float pitch = 1f, bool repeat = false) => _gameApi.Sounds.Play(file, volume, pitch, repeat);
        public void PlaySoundAt(string file, long x, long y, long z, float volume = 100f, float pitch = 1f, float falloff = 1f) => _gameApi.Sounds.PlayAt(file, x, y, z, volume, pitch, falloff);
        public void PlaySoundOn(string file, GameObject obj, float volume = 100f, float pitch = 1f, float falloff = 1f) => _gameApi.Sounds.PlayOn(file, obj, volume, pitch, falloff);
        public void StopSound(string file) => _gameApi.Sounds.Stop(file);
        public void StopSoundOn(string file, GameObject obj) => _gameApi.Sounds.StopOn(file, obj);
    }
}
